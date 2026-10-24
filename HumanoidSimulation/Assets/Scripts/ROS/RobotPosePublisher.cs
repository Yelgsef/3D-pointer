using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RobotPosePublisher : MonoBehaviour
{
    [SerializeField] private string poseTopic = "/robot/pose";
    [SerializeField, Min(1f)] private float publishRate = 10f;

    private ROSConnection ros;
    private float publishInterval;
    private float nextPublishTime;

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

        publishInterval = 1f / publishRate;
        nextPublishTime = Time.time;
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
    }
}
