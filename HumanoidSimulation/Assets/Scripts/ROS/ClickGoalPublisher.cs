using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ClickGoalPublisher : MonoBehaviour
{
    [SerializeField] private Camera eventCamera;
    [SerializeField] private string goalTopic = "/goal_pose";
    [SerializeField] private string cancelTopic = "/goal_cancel";
    [SerializeField] private string frameId = "map";
    [SerializeField] private float groundPlaneY;
    [SerializeField] private Vector2 groundCenter = new Vector2(0f, 1f);
    [SerializeField] private Vector2 groundHalfExtents = new Vector2(7f, 12f);

    private ROSConnection ros;

    private void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(goalTopic);
        ros.RegisterPublisher<BoolMsg>(cancelTopic);

        if (eventCamera == null)
            eventCamera = Camera.main;
    }

    private void Update()
    {
        if (ros == null || Mouse.current == null)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ros.Publish(cancelTopic, new BoolMsg(true));
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame || eventCamera == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = eventCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
        if (!ground.Raycast(ray, out float distance))
            return;

        Vector3 destination = ray.GetPoint(distance);
        if (Mathf.Abs(destination.x - groundCenter.x) > groundHalfExtents.x ||
            Mathf.Abs(destination.z - groundCenter.y) > groundHalfExtents.y)
            return;

        Vector3<FLU> position = destination.To<FLU>();
        DateTime utcNow = DateTime.UtcNow;
        long seconds = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
        uint nanoseconds = (uint)(utcNow.Ticks % TimeSpan.TicksPerSecond * 100);
        var header = new HeaderMsg(new TimeMsg((int)seconds, nanoseconds), frameId);
        var pose = new PoseMsg(
            new PointMsg(position.x, position.y, position.z),
            Quaternion.identity.To<FLU>());
        ros.Publish(goalTopic, new PoseStampedMsg(header, pose));
        Debug.Log($"Published navigation goal at ROS ({position.x:F2}, {position.y:F2}) in {frameId}.");
    }
}
