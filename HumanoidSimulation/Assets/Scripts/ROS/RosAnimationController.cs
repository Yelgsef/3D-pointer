using System;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class RosAnimationController : MonoBehaviour
{
    [Header("ROS")]
    [SerializeField] private string commandTopic = "/cmd_vel";

    [Header("Motion limits")]
    [SerializeField, Min(0.01f)] private float maximumLinearSpeed = 3f;
    [SerializeField, Min(0.01f)] private float maximumYawRate = 1.57f;
    [SerializeField, Min(0.01f)] private float acceleration = 4f;
    [SerializeField, Min(0.01f)] private float deceleration = 4f;
    [SerializeField, Min(0.05f)] private float commandTimeout = 0.7f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private Animator animator;
    private ROSConnection ros;
    private float targetLinear;
    private float targetYawRate;
    private float currentLinear;
    private float currentYawRate;
    private float lastCommandTime = float.NegativeInfinity;
    private float verticalVelocity;
    private bool subscribed;

    public float CurrentLinearSpeed => currentLinear;
    public float CurrentYawRate => currentYawRate;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TwistMsg>(commandTopic, ReceiveCommand);
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && ros != null)
            ros.Unsubscribe(commandTopic);

        subscribed = false;
        targetLinear = 0f;
        targetYawRate = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        bool commandIsFresh = Time.time - lastCommandTime <= commandTimeout;
        float desiredLinear = commandIsFresh ? targetLinear : 0f;
        float desiredYawRate = commandIsFresh ? targetYawRate : 0f;

        currentLinear = Mathf.MoveTowards(
            currentLinear,
            desiredLinear,
            (Mathf.Abs(desiredLinear) < Mathf.Abs(currentLinear) ? deceleration : acceleration) * deltaTime);
        currentYawRate = Mathf.MoveTowards(currentYawRate, desiredYawRate, acceleration * deltaTime);

        // ROS FLU positive yaw turns left; Unity positive Y yaw turns right.
        transform.Rotate(Vector3.up, -currentYawRate * Mathf.Rad2Deg * deltaTime, Space.World);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * deltaTime;

        Vector3 velocity = transform.forward * currentLinear + Vector3.up * verticalVelocity;
        characterController.Move(velocity * deltaTime);

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Clamp01(Mathf.Abs(currentLinear) / maximumLinearSpeed));
            animator.SetFloat("MoveDirection", Mathf.Sign(currentLinear));
            animator.SetFloat("Turn", Mathf.Clamp(currentYawRate / maximumYawRate, -1f, 1f));
        }
    }

    private void ReceiveCommand(TwistMsg message)
    {
        if (message?.linear == null || message.angular == null ||
            double.IsNaN(message.linear.x) || double.IsInfinity(message.linear.x) ||
            double.IsNaN(message.angular.z) || double.IsInfinity(message.angular.z))
            return;

        targetLinear = Mathf.Clamp((float)message.linear.x, -maximumLinearSpeed, maximumLinearSpeed);
        targetYawRate = Mathf.Clamp((float)message.angular.z, -maximumYawRate, maximumYawRate);
        lastCommandTime = Time.time;
    }
}
