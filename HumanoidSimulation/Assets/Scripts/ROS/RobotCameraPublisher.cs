using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Collections;
using Unity.Robotics.ROSTCPConnector;
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
    [SerializeField, Min(0.1f)] private float publishRate = 5f;

    private Camera captureCamera;
    private ROSConnection ros;
    private MessagePool<ImageMsg> imagePool;
    private MessagePool<CameraInfoMsg> cameraInfoPool;
    private Texture2D synchronousReadTexture;
    private float nextCaptureTime;
    private bool readbackPending;
    private bool destroying;
    private bool hasWarnedReadback;

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

        if (!SystemInfo.supportsAsyncGPUReadback && synchronousReadTexture == null)
            synchronousReadTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGB24, false);

        return true;
    }

    private void EnsureRosPublishers()
    {
        ros = ROSConnection.GetOrCreateInstance();
        if (imagePool == null)
            imagePool = new MessagePool<ImageMsg>(2);
        if (cameraInfoPool == null)
            cameraInfoPool = new MessagePool<CameraInfoMsg>(2);

        var imageTopicState = ros.GetTopic(imageTopic);
        if (imageTopicState == null || !imageTopicState.IsPublisher)
            ros.RegisterPublisher<ImageMsg>(imageTopic, 1);
        var cameraInfoTopicState = ros.GetTopic(cameraInfoTopic);
        if (cameraInfoTopicState == null || !cameraInfoTopicState.IsPublisher)
            ros.RegisterPublisher<CameraInfoMsg>(cameraInfoTopic, 1);

        ros.GetTopic(imageTopic).SetMessagePool(imagePool);
        ros.GetTopic(cameraInfoTopic).SetMessagePool(cameraInfoPool);
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
        if (ros == null || imagePool == null || cameraInfoPool == null)
            EnsureRosPublishers();
        if (readbackPending || Time.unscaledTime < nextCaptureTime)
            return;

        nextCaptureTime = Time.unscaledTime + 1f / Mathf.Max(0.1f, publishRate);
        DateTime acquisitionTimeUtc = DateTime.UtcNow;

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            readbackPending = true;
            try
            {
                AsyncGPUReadback.Request(sourceTexture, 0, TextureFormat.RGB24,
                    request => CompleteReadback(request, acquisitionTimeUtc));
            }
            catch (Exception exception)
            {
                readbackPending = false;
                WarnReadbackFailure("Unable to request asynchronous GPU readback: " + exception.Message);
            }
            return;
        }

        PublishSynchronousReadback(acquisitionTimeUtc);
    }

    private void CompleteReadback(AsyncGPUReadbackRequest request, DateTime acquisitionTimeUtc)
    {
        readbackPending = false;
        if (destroying || this == null)
            return;
        if (request.hasError)
        {
            WarnReadbackFailure("Unity reported an error while reading RobotEyeRT.");
            return;
        }

        NativeArray<byte> pixels = request.GetData<byte>();
        PublishPixels(pixels, acquisitionTimeUtc);
    }

    private void PublishSynchronousReadback(DateTime acquisitionTimeUtc)
    {
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = sourceTexture;
            synchronousReadTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            synchronousReadTexture.Apply(false, false);
            PublishPixels(synchronousReadTexture.GetRawTextureData<byte>(), acquisitionTimeUtc);
        }
        catch (Exception exception)
        {
            WarnReadbackFailure("Unable to read RobotEyeRT synchronously: " + exception.Message);
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private void PublishPixels(NativeArray<byte> pixels, DateTime acquisitionTimeUtc)
    {
        if (ros == null || imagePool == null || cameraInfoPool == null)
        {
            WarnReadbackFailure("ROS publishers were unavailable when a camera frame completed.");
            return;
        }

        int width = sourceTexture.width;
        int height = sourceTexture.height;
        int rowBytes = width * 3;
        int byteCount = rowBytes * height;
        if (pixels.Length != byteCount)
        {
            WarnReadbackFailure("RGB24 readback size was " + pixels.Length + ", expected " + byteCount + " bytes.");
            return;
        }

        ImageMsg image = imagePool.GetOrCreateMessage();
        if (image.data == null || image.data.Length != byteCount)
            image.data = new byte[byteCount];

        // ROS images start at the top-left; Unity texture readback starts at the bottom-left.
        for (int row = 0; row < height; row++)
        {
            NativeArray<byte>.Copy(pixels, (height - row - 1) * rowBytes, image.data, row * rowBytes, rowBytes);
        }

        TimeMsg stamp = CreateRosTimestamp(acquisitionTimeUtc);
        image.header = new HeaderMsg(stamp, OpticalFrameId);
        image.height = (uint)height;
        image.width = (uint)width;
        image.encoding = "rgb8";
        image.is_bigendian = 0;
        image.step = (uint)rowBytes;

        CameraInfoMsg cameraInfo = cameraInfoPool.GetOrCreateMessage();
        FillCameraInfo(cameraInfo, stamp, width, height);

        ros.Publish(imageTopic, image);
        ros.Publish(cameraInfoTopic, cameraInfo);
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
    }
}
