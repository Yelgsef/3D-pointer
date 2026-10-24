using System;
using RosMessageTypes.BuiltinInterfaces;
using GeometryMessages = RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

/// <summary>Publishes Unity-only evaluation labels on isolated simulation topics.</summary>
public sealed class SimulationGroundTruthPublisher : MonoBehaviour
{
    [SerializeField] private Transform humanTarget;
    [SerializeField] private Transform robotCamera;
    [SerializeField] private Transform targetObjectA;
    [SerializeField] private Transform targetObjectB;
    [SerializeField, Min(1f)] private float publishRate = 10f;
    [SerializeField] private string worldFrame = "map";

    private ROSConnection ros;
    private Animator animator;
    private HumanTargetGestureController gestures;
    private Camera cameraComponent;
    private Transform[] rigTransforms;
    private float nextPublishTime;

    private static readonly HumanBodyBones[] JointOrder =
    {
        HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
        HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftHand, HumanBodyBones.RightHand
    };

    private void OnEnable()
    {
        ros = ROSConnection.GetOrCreateInstance();
        Register<GeometryMessages.PoseArrayMsg>("/simulation/ground_truth/human_joints");
        Register<GeometryMessages.PoseArrayMsg>("/simulation/ground_truth/objects");
        Register<GeometryMessages.PoseStampedMsg>("/simulation/ground_truth/pointing_ray");
        Register<StringMsg>("/simulation/ground_truth/pointing_label");
        Register<StringMsg>("/simulation/ground_truth/camera_visibility");

        if (humanTarget == null) humanTarget = GameObject.Find("HumanTarget")?.transform;
        if (humanTarget != null)
        {
            animator = humanTarget.GetComponentInChildren<Animator>();
            gestures = humanTarget.GetComponent<HumanTargetGestureController>();
            if (animator != null) rigTransforms = animator.GetComponentsInChildren<Transform>(true);
        }
        if (targetObjectA == null) targetObjectA = GameObject.Find("TargetObjectA")?.transform;
        if (targetObjectB == null) targetObjectB = GameObject.Find("TargetObjectB")?.transform;
        if (robotCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera candidate in cameras)
                if (candidate.name == "RobotEyeCamera") { robotCamera = candidate.transform; break; }
        }
        if (robotCamera != null) cameraComponent = robotCamera.GetComponent<Camera>();
        nextPublishTime = Time.unscaledTime;
    }

    private void Register<T>(string topic) where T : Message, new()
    {
        var existing = ros.GetTopic(topic);
        if (existing == null || !existing.IsPublisher) ros.RegisterPublisher<T>(topic);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPublishTime) return;
        nextPublishTime = Time.unscaledTime + 1f / publishRate;
        DateTime now = DateTime.UtcNow;
        TimeMsg stamp = ToRosTime(now);
        PublishJoints(stamp);
        PublishObjects(stamp);
        PublishPointing(stamp, now);
        PublishVisibility(stamp, now);
    }

    private void PublishJoints(TimeMsg stamp)
    {
        var poses = new GeometryMessages.PoseMsg[JointOrder.Length];
        for (int i = 0; i < JointOrder.Length; i++)
        {
            Transform bone = FindBone(JointOrder[i]);
            Vector3 point = bone != null ? bone.position : humanTarget.position;
            if (JointOrder[i] == HumanBodyBones.Head && bone != null)
                point += bone.up * 0.08f; // Derived eye/head reference; this rig has no eye bones.
            poses[i] = new GeometryMessages.PoseMsg(
                ToPoint(point),
                new GeometryMessages.QuaternionMsg(0, 0, 0, 1));
        }
        ros.Publish("/simulation/ground_truth/human_joints",
            new GeometryMessages.PoseArrayMsg(new HeaderMsg(stamp, worldFrame), poses));
    }

    private void PublishObjects(TimeMsg stamp)
    {
        Transform[] objects = { targetObjectA, targetObjectB };
        var poses = new GeometryMessages.PoseMsg[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            Transform item = objects[i];
            poses[i] = item == null
                ? new GeometryMessages.PoseMsg()
                : new GeometryMessages.PoseMsg(ToPoint(item.position), ToQuaternion(item.rotation));
        }
        ros.Publish("/simulation/ground_truth/objects",
            new GeometryMessages.PoseArrayMsg(new HeaderMsg(stamp, worldFrame), poses));
    }

    private void PublishPointing(TimeMsg stamp, DateTime now)
    {
        Transform head = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Head) : humanTarget;
        Vector3 origin = head != null ? head.position + head.up * 0.08f : humanTarget.position;
        Vector3 direction = Vector3.forward;
        string arm = "none";
        string targetId = "";
        if (gestures != null)
        {
            if (gestures.CurrentGesture == HumanTargetGesture.PointLeft)
            {
                arm = "left";
                Transform wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                direction = wrist != null ? (wrist.position - origin).normalized : direction;
            }
            else if (gestures.CurrentGesture == HumanTargetGesture.PointRight)
            {
                arm = "right";
                Transform wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                direction = wrist != null ? (wrist.position - origin).normalized : direction;
            }
            if (gestures.CurrentTarget == targetObjectA) targetId = "TargetObjectA";
            else if (gestures.CurrentTarget == targetObjectB) targetId = "TargetObjectB";
        }

        Quaternion orientation = Quaternion.LookRotation(direction, Vector3.up);
        ros.Publish("/simulation/ground_truth/pointing_ray",
            new GeometryMessages.PoseStampedMsg(new HeaderMsg(stamp, worldFrame),
                new GeometryMessages.PoseMsg(ToPoint(origin), ToQuaternion(orientation))));
        string label = string.Format("{{\"stamp_utc\":\"{0:O}\",\"frame_id\":\"{1}\",\"arm\":\"{2}\",\"intended_target_id\":\"{3}\"}}",
            now, worldFrame, arm, targetId);
        ros.Publish("/simulation/ground_truth/pointing_label", new StringMsg(label));
    }

    private void PublishVisibility(TimeMsg stamp, DateTime now)
    {
        bool visible = false;
        if (cameraComponent != null && humanTarget != null)
        {
            Vector3 point = humanTarget.position + Vector3.up * 1.2f;
            Vector3 viewport = cameraComponent.WorldToViewportPoint(point);
            visible = viewport.z > cameraComponent.nearClipPlane && viewport.z < cameraComponent.farClipPlane &&
                      viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
            if (visible && Physics.Raycast(robotCamera.position, point - robotCamera.position,
                    out RaycastHit hit, Vector3.Distance(robotCamera.position, point)))
                visible = hit.transform == humanTarget || hit.transform.IsChildOf(humanTarget);
        }
        string value = string.Format("{{\"stamp_utc\":\"{0:O}\",\"frame_id\":\"{1}\",\"human_visible\":{2}}}",
            now, worldFrame, visible ? "true" : "false");
        ros.Publish("/simulation/ground_truth/camera_visibility", new StringMsg(value));
    }

    private static TimeMsg ToRosTime(DateTime utc)
    {
        long ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
        return new TimeMsg((int)(ticks / TimeSpan.TicksPerSecond),
            (uint)((ticks % TimeSpan.TicksPerSecond) * 100L));
    }

    private static GeometryMessages.PointMsg ToPoint(Vector3 position)
    {
        Vector3<FLU> converted = position.To<FLU>();
        return new GeometryMessages.PointMsg(converted.x, converted.y, converted.z);
    }

    private static GeometryMessages.QuaternionMsg ToQuaternion(Quaternion rotation)
    {
        Quaternion<FLU> converted = rotation.To<FLU>();
        return new GeometryMessages.QuaternionMsg(converted.x, converted.y, converted.z, converted.w);
    }

    private Transform FindBone(HumanBodyBones bone)
    {
        if (animator == null) return null;
        Transform mapped = animator.isHuman ? animator.GetBoneTransform(bone) : null;
        if (mapped != null) return mapped;

        string expectedName = bone.ToString();
        foreach (Transform candidate in rigTransforms)
            if (candidate.name == expectedName) return candidate;
        return null;
    }
}
