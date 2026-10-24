using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Collections;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class RobotCameraPublisher : MonoBehaviour
{
    private const string OpticalFrameId = "robot_camera_optical_frame";

    [SerializeField] private RenderTexture sourceTexture;
    [SerializeField] private string imageTopic = "/camera/image_raw";
    [SerializeField] private string cameraInfoTopic = "/camera/camera_info";
    [SerializeField] private string depthImageTopic = "/camera/depth/image_raw";
    [SerializeField, Min(0.1f)] private float publishRate = 5f;

    private Camera captureCamera;
    private ROSConnection ros;
    private MessagePool<ImageMsg> imagePool;
    private MessagePool<CameraInfoMsg> cameraInfoPool;
    private MessagePool<ImageMsg> depthImagePool;
    private Texture2D synchronousReadTexture;
    private Texture2D synchronousDepthReadTexture;
    private RenderTexture linearDepthTexture;
    private Material linearDepthMaterial;
    private DepthTextureMode originalDepthTextureMode;
    private float nextCaptureTime;
    private bool readbackPending;
    private bool destroying;
    private bool hasWarnedReadback;
    private bool hasOriginalDepthTextureMode;
    private int activeCaptureId;
    private int pendingCompletions;
    private byte[] pendingRgbPixels;
    private float[] pendingDepthPixels;
    private TimeMsg pendingStamp;

    private void Awake()
    {
        if (!ConfigureCapture())
            enabled = false;
    }

    private bool ConfigureCapture()
    {
        captureCamera = GetComponent<Camera>();
        if (captureCamera == null)
        {
            Debug.LogError("RobotCameraPublisher requires its existing RobotEyeCamera.", this);
            return false;
        }

        if (sourceTexture == null)
            sourceTexture = captureCamera.targetTexture;

        if (sourceTexture == null || captureCamera.targetTexture != sourceTexture)
        {
            Debug.LogError("RobotCameraPublisher must use the existing RobotEyeCamera RenderTexture.", this);
            return false;
        }

        if (captureCamera.orthographic)
        {
            Debug.LogError("RobotCameraPublisher requires a perspective camera for pinhole intrinsics.", this);
            return false;
        }

        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
        {
            Debug.LogError("This graphics device cannot create the RFloat metric depth target.", this);
            return false;
        }

        Shader depthShader = Shader.Find("Hidden/RobotCamera/LinearDepth");
        if (depthShader == null)
        {
            Debug.LogError("Could not find Hidden/RobotCamera/LinearDepth shader.", this);
            return false;
        }

        if (linearDepthMaterial == null)
            linearDepthMaterial = new Material(depthShader) { hideFlags = HideFlags.HideAndDontSave };
        if (linearDepthTexture == null || linearDepthTexture.width != sourceTexture.width ||
            linearDepthTexture.height != sourceTexture.height)
        {
            if (linearDepthTexture != null)
            {
                linearDepthTexture.Release();
                Destroy(linearDepthTexture);
            }

            linearDepthTexture = new RenderTexture(
                sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear)
            {
                name = "RobotEyeLinearDepth",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            linearDepthTexture.Create();
        }

        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            if (synchronousReadTexture == null)
                synchronousReadTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGB24, false);
            if (synchronousDepthReadTexture == null)
                synchronousDepthReadTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RFloat, false, true);
        }

        if (!hasOriginalDepthTextureMode)
        {
            originalDepthTextureMode = captureCamera.depthTextureMode;
            hasOriginalDepthTextureMode = true;
        }
        captureCamera.depthTextureMode |= DepthTextureMode.Depth;
        return true;
    }

    private void EnsureRosPublishers()
    {
        ros = ROSConnection.GetOrCreateInstance();
        if (imagePool == null)
            imagePool = new MessagePool<ImageMsg>(2);
        if (cameraInfoPool == null)
            cameraInfoPool = new MessagePool<CameraInfoMsg>(2);
        if (depthImagePool == null)
            depthImagePool = new MessagePool<ImageMsg>(2);

        EnsurePublisher(imageTopic, imagePool, () => ros.RegisterPublisher<ImageMsg>(imageTopic, 1));
        EnsurePublisher(cameraInfoTopic, cameraInfoPool, () => ros.RegisterPublisher<CameraInfoMsg>(cameraInfoTopic, 1));
        EnsurePublisher(depthImageTopic, depthImagePool, () => ros.RegisterPublisher<ImageMsg>(depthImageTopic, 1));
    }

    private void EnsurePublisher<T>(string topicName, MessagePool<T> pool, Action registerPublisher)
        where T : Message, new()
    {
        var topicState = ros.GetTopic(topicName);
        if (topicState == null || !topicState.IsPublisher)
            registerPublisher();
        ros.GetTopic(topicName).SetMessagePool(pool);
    }

    private void OnEnable()
    {
        if (!ConfigureCapture())
        {
            enabled = false;
            return;
        }

        EnsureRosPublishers();
        readbackPending = false;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (captureCamera != null && hasOriginalDepthTextureMode)
            captureCamera.depthTextureMode = originalDepthTextureMode;
        readbackPending = false;
        pendingRgbPixels = null;
        pendingDepthPixels = null;
    }

    private void OnPostRender()
    {
        CaptureFrame();
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == captureCamera)
            CaptureFrame();
    }

    private void CaptureFrame()
    {
        if (ros == null || imagePool == null || cameraInfoPool == null || depthImagePool == null)
            EnsureRosPublishers();
        if (readbackPending || Time.unscaledTime < nextCaptureTime)
            return;

        nextCaptureTime = Time.unscaledTime + 1f / Mathf.Max(0.1f, publishRate);
        TimeMsg stamp = CreateRosTimestamp(DateTime.UtcNow);
        Graphics.Blit(Texture2D.blackTexture, linearDepthTexture, linearDepthMaterial);

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            activeCaptureId++;
            int captureId = activeCaptureId;
            pendingStamp = stamp;
            pendingCompletions = 0;
            pendingRgbPixels = null;
            pendingDepthPixels = null;
            readbackPending = true;
            try
            {
                AsyncGPUReadback.Request(sourceTexture, 0, TextureFormat.RGB24,
                    request => CompleteRgbReadback(request, captureId));
                AsyncGPUReadback.Request(linearDepthTexture, 0, TextureFormat.RFloat,
                    request => CompleteDepthReadback(request, captureId));
            }
            catch (Exception exception)
            {
                FailPendingFrame(captureId, "Unable to request asynchronous camera readback: " + exception.Message);
            }
            return;
        }

        PublishSynchronousReadback(stamp);
    }

    private void CompleteRgbReadback(AsyncGPUReadbackRequest request, int captureId)
    {
        if (!IsCurrentCapture(captureId))
            return;
        if (request.hasError)
        {
            FailPendingFrame(captureId, "Unity reported an error while reading RobotEyeRT.");
            return;
        }

        NativeArray<byte> pixels = request.GetData<byte>();
        int width = sourceTexture.width;
        int height = sourceTexture.height;
        int rowBytes = width * 3;
        if (pixels.Length != rowBytes * height)
        {
            FailPendingFrame(captureId, "RGB readback returned an unexpected buffer size.");
            return;
        }

        pendingRgbPixels = new byte[pixels.Length];
        for (int row = 0; row < height; row++)
            NativeArray<byte>.Copy(pixels, (height - row - 1) * rowBytes, pendingRgbPixels, row * rowBytes, rowBytes);
        CompleteReadback(captureId);
    }

    private void CompleteDepthReadback(AsyncGPUReadbackRequest request, int captureId)
    {
        if (!IsCurrentCapture(captureId))
            return;
        if (request.hasError)
        {
            FailPendingFrame(captureId, "Unity reported an error while reading the linear depth target.");
            return;
        }

        NativeArray<float> pixels = request.GetData<float>();
        int width = sourceTexture.width;
        int height = sourceTexture.height;
        if (pixels.Length != width * height)
        {
            FailPendingFrame(captureId, "Depth readback returned an unexpected buffer size.");
            return;
        }

        pendingDepthPixels = new float[pixels.Length];
        for (int row = 0; row < height; row++)
        {
            int sourceOffset = (height - row - 1) * width;
            int destinationOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                float depth = pixels[sourceOffset + column];
                pendingDepthPixels[destinationOffset + column] =
                    float.IsNaN(depth) || float.IsInfinity(depth) || depth <= 0f || depth >= captureCamera.farClipPlane
                        ? 0f : depth;
            }
        }
        CompleteReadback(captureId);
    }

    private bool IsCurrentCapture(int captureId)
    {
        // Async GPU callbacks can arrive after Unity destroys this component during a reload.
        if (this == null)
            return false;
        return !destroying && isActiveAndEnabled && readbackPending && captureId == activeCaptureId;
    }

    private void CompleteReadback(int captureId)
    {
        if (!IsCurrentCapture(captureId))
            return;
        pendingCompletions++;
        if (pendingCompletions < 2)
            return;

        readbackPending = false;
        PublishPixels(pendingRgbPixels, pendingDepthPixels, pendingStamp);
        pendingRgbPixels = null;
        pendingDepthPixels = null;
    }

    private void FailPendingFrame(int captureId, string message)
    {
        if (this == null || captureId != activeCaptureId)
            return;
        readbackPending = false;
        pendingRgbPixels = null;
        pendingDepthPixels = null;
        WarnReadbackFailure(message);
    }

    private void PublishSynchronousReadback(TimeMsg stamp)
    {
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = sourceTexture;
            synchronousReadTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            synchronousReadTexture.Apply(false, false);
            byte[] rgbPixels = CopyFlippedRgb(synchronousReadTexture.GetRawTextureData<byte>());

            RenderTexture.active = linearDepthTexture;
            synchronousDepthReadTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            synchronousDepthReadTexture.Apply(false, false);
            float[] depthPixels = CopyFlippedDepth(synchronousDepthReadTexture.GetRawTextureData<float>());
            PublishPixels(rgbPixels, depthPixels, stamp);
        }
        catch (Exception exception)
        {
            WarnReadbackFailure("Unable to read the camera color/depth targets synchronously: " + exception.Message);
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private byte[] CopyFlippedRgb(NativeArray<byte> pixels)
    {
        int width = sourceTexture.width;
        int height = sourceTexture.height;
        int rowBytes = width * 3;
        byte[] result = new byte[rowBytes * height];
        for (int row = 0; row < height; row++)
            NativeArray<byte>.Copy(pixels, (height - row - 1) * rowBytes, result, row * rowBytes, rowBytes);
        return result;
    }

    private float[] CopyFlippedDepth(NativeArray<float> pixels)
    {
        int width = sourceTexture.width;
        int height = sourceTexture.height;
        float[] result = new float[width * height];
        for (int row = 0; row < height; row++)
        {
            int sourceOffset = (height - row - 1) * width;
            int destinationOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                float depth = pixels[sourceOffset + column];
                result[destinationOffset + column] =
                    float.IsNaN(depth) || float.IsInfinity(depth) || depth <= 0f || depth >= captureCamera.farClipPlane
                        ? 0f : depth;
            }
        }
        return result;
    }

    private void PublishPixels(byte[] rgbPixels, float[] depthPixels, TimeMsg stamp)
    {
        if (ros == null || imagePool == null || cameraInfoPool == null || depthImagePool == null)
        {
            WarnReadbackFailure("ROS publishers were unavailable when a camera frame completed.");
            return;
        }

        int width = sourceTexture.width;
        int height = sourceTexture.height;
        int rgbBytes = width * height * 3;
        int depthBytes = width * height * sizeof(float);
        if (rgbPixels == null || rgbPixels.Length != rgbBytes ||
            depthPixels == null || depthPixels.Length != width * height)
        {
            WarnReadbackFailure("Camera readback sizes did not match the configured image dimensions.");
            return;
        }

        ImageMsg image = imagePool.GetOrCreateMessage();
        image.data = EnsureBuffer(image.data, rgbBytes);
        Buffer.BlockCopy(rgbPixels, 0, image.data, 0, rgbBytes);
        image.header = new HeaderMsg(stamp, OpticalFrameId);
        image.height = (uint)height;
        image.width = (uint)width;
        image.encoding = "rgb8";
        image.is_bigendian = 0;
        image.step = (uint)(width * 3);

        ImageMsg depth = depthImagePool.GetOrCreateMessage();
        depth.data = EnsureBuffer(depth.data, depthBytes);
        Buffer.BlockCopy(depthPixels, 0, depth.data, 0, depthBytes);
        depth.header = new HeaderMsg(stamp, OpticalFrameId);
        depth.height = (uint)height;
        depth.width = (uint)width;
        depth.encoding = "32FC1";
        depth.is_bigendian = BitConverter.IsLittleEndian ? (byte)0 : (byte)1;
        depth.step = (uint)(width * sizeof(float));

        CameraInfoMsg cameraInfo = cameraInfoPool.GetOrCreateMessage();
        FillCameraInfo(cameraInfo, stamp, width, height);

        ros.Publish(imageTopic, image);
        ros.Publish(depthImageTopic, depth);
        ros.Publish(cameraInfoTopic, cameraInfo);
    }

    private static byte[] EnsureBuffer(byte[] data, int length)
    {
        return data != null && data.Length == length ? data : new byte[length];
    }

    private void FillCameraInfo(CameraInfoMsg message, TimeMsg stamp, int width, int height)
    {
        double fy = height / (2.0 * Math.Tan(captureCamera.fieldOfView * Mathf.Deg2Rad * 0.5));
        double fx = fy;
        double cx = width * 0.5;
        double cy = height * 0.5;

        message.header = new HeaderMsg(stamp, OpticalFrameId);
        message.height = (uint)height;
        message.width = (uint)width;
        message.distortion_model = "plumb_bob";
        if (message.d == null || message.d.Length != 5) message.d = new double[5];
        if (message.k == null || message.k.Length != 9) message.k = new double[9];
        if (message.r == null || message.r.Length != 9) message.r = new double[9];
        if (message.p == null || message.p.Length != 12) message.p = new double[12];
        Array.Clear(message.d, 0, message.d.Length);
        Array.Clear(message.k, 0, message.k.Length);
        Array.Clear(message.r, 0, message.r.Length);
        Array.Clear(message.p, 0, message.p.Length);

        message.k[0] = fx;
        message.k[2] = cx;
        message.k[4] = fy;
        message.k[5] = cy;
        message.k[8] = 1.0;

        message.r[0] = 1.0;
        message.r[4] = 1.0;
        message.r[8] = 1.0;

        message.p[0] = fx;
        message.p[2] = cx;
        message.p[5] = fy;
        message.p[6] = cy;
        message.p[10] = 1.0;
        message.binning_x = 0;
        message.binning_y = 0;
        message.roi = message.roi ?? new RegionOfInterestMsg();
    }

    private static TimeMsg CreateRosTimestamp(DateTime acquisitionTimeUtc)
    {
        long unixTicks = acquisitionTimeUtc.Ticks - DateTime.UnixEpoch.Ticks;
        int seconds = (int)(unixTicks / TimeSpan.TicksPerSecond);
        uint nanoseconds = (uint)((unixTicks % TimeSpan.TicksPerSecond) * 100L);
        return new TimeMsg(seconds, nanoseconds);
    }

    private void WarnReadbackFailure(string message)
    {
        if (hasWarnedReadback)
            return;

        hasWarnedReadback = true;
        Debug.LogWarning(message, this);
    }

    private void OnDestroy()
    {
        destroying = true;
        if (synchronousReadTexture != null)
            Destroy(synchronousReadTexture);
        if (synchronousDepthReadTexture != null)
            Destroy(synchronousDepthReadTexture);
        if (linearDepthTexture != null)
        {
            linearDepthTexture.Release();
            Destroy(linearDepthTexture);
        }
        if (linearDepthMaterial != null)
            Destroy(linearDepthMaterial);
    }
}
