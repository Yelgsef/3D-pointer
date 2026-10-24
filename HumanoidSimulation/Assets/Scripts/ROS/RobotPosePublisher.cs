using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RobotPosePublisher : MonoBehaviour
{
    [SerializeField] private string poseTopic = "/robot/pose";
    [SerializeField] private string tfTopic = "/tf";
    [SerializeField] private string staticTfInputTopic = "/unity/tf_static";
    [SerializeField] private string mapFrame = "map";
    [SerializeField] private string baseFrame = "base_link";
    [SerializeField] private string cameraOpticalFrame = "robot_camera_optical_frame";
    [SerializeField, Min(1f)] private float publishRate = 10f;
    [SerializeField, Min(0.1f)] private float staticTransformPublishRate = 1f;

    private ROSConnection ros;
    private float publishInterval;
    private float nextPublishTime;
    private float nextStaticPublishTime;
    private Transform cameraTransform;

    private void OnEnable()
    {
        EnsurePublisher();
    }

    private void EnsurePublisher()
    {
        if (ros == null)
            ros = ROSConnection.GetOrCreateInstance();

        var topic = ros.GetTopic(poseTopic);
        if (topic == null || !topic.IsPublisher)
            ros.RegisterPublisher<PoseMsg>(poseTopic);

        RegisterPublisher<TFMessageMsg>(tfTopic);
        RegisterPublisher<TFMessageMsg>(staticTfInputTopic);
        if (cameraTransform == null)
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            foreach (Camera candidate in cameras)
            {
                if (candidate.name == "RobotEyeCamera")
                {
                    cameraTransform = candidate.transform;
                    break;
                }
            }
        }

        publishInterval = 1f / publishRate;
        nextPublishTime = Time.time;
        nextStaticPublishTime = Time.unscaledTime;
    }

    private void RegisterPublisher<T>(string topicName) where T : Message, new()
    {
        var topic = ros.GetTopic(topicName);
        if (topic == null || !topic.IsPublisher)
            ros.RegisterPublisher<T>(topicName);
    }

    private void Update()
    {
        if (ros == null || ros.GetTopic(poseTopic) == null || !ros.GetTopic(poseTopic).IsPublisher)
            EnsurePublisher();
        if (Time.time < nextPublishTime)
            return;

        nextPublishTime = Time.time + publishInterval;
        Vector3<FLU> position = transform.position.To<FLU>();
        Quaternion<FLU> orientation = transform.rotation.To<FLU>();
        var pose = new PoseMsg(
            new PointMsg(position.x, position.y, position.z),
            new QuaternionMsg(orientation.x, orientation.y, orientation.z, orientation.w));
        ros.Publish(poseTopic, pose);

        TimeMsg stamp = CreateRosTimestamp(DateTime.UtcNow);
        var baseTransform = new TransformStampedMsg(
            new HeaderMsg(stamp, mapFrame),
            baseFrame,
            new TransformMsg(
                new Vector3Msg(position.x, position.y, position.z),
                new QuaternionMsg(orientation.x, orientation.y, orientation.z, orientation.w)));
        ros.Publish(tfTopic, new TFMessageMsg(new[] { baseTransform }));

        if (cameraTransform != null && Time.unscaledTime >= nextStaticPublishTime)
        {
            nextStaticPublishTime = Time.unscaledTime + 1f / staticTransformPublishRate;
            PublishStaticCameraTransform(stamp);
        }
    }

    private void PublishStaticCameraTransform(TimeMsg stamp)
    {
        Vector3<FLU> translation = transform.InverseTransformPoint(cameraTransform.position).To<FLU>();
        Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * cameraTransform.rotation;
        Quaternion<FLU> cameraRotation = relativeRotation.To<FLU>();

        // Camera optical axes are right/down/forward; base_link uses forward/left/up.
        var opticalRotation = new Quaternion<FLU>(0.5f, -0.5f, 0.5f, -0.5f);
        Quaternion<FLU> rotation = cameraRotation * opticalRotation;
        var cameraTransformMessage = new TransformStampedMsg(
            new HeaderMsg(stamp, baseFrame),
            cameraOpticalFrame,
            new TransformMsg(
                new Vector3Msg(translation.x, translation.y, translation.z),
                new QuaternionMsg(rotation.x, rotation.y, rotation.z, rotation.w)));
        ros.Publish(staticTfInputTopic, new TFMessageMsg(new[] { cameraTransformMessage }));
    }

    private static TimeMsg CreateRosTimestamp(DateTime timeUtc)
    {
        long ticks = timeUtc.Ticks - DateTime.UnixEpoch.Ticks;
        return new TimeMsg((int)(ticks / TimeSpan.TicksPerSecond),
            (uint)((ticks % TimeSpan.TicksPerSecond) * 100L));
    }
}
