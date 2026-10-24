using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RobotObserverScenarioControls : MonoBehaviour
{
    [SerializeField] private Transform robotObserver;
    [SerializeField] private Transform humanTarget;
    [SerializeField] private Transform obstacle;
    [SerializeField] private Animator humanAnimator;
    [SerializeField] private HumanTargetGestureController humanGestures;
    [SerializeField] private Transform targetObjectA;
    [SerializeField] private Transform targetObjectB;

    private Vector3 robotStart;
    private Quaternion robotStartRotation;
    private Vector3 humanStart;
    private Quaternion humanStartRotation;
    private Vector3 obstacleStart;
    private Vector3 obstacleStartScale;
    private bool turningToHuman;

    private void Awake()
    {
        if (robotObserver == null) robotObserver = transform;
        if (humanTarget != null) { humanStart = humanTarget.position; humanStartRotation = humanTarget.rotation; }
        robotStart = robotObserver.position;
        robotStartRotation = robotObserver.rotation;
        if (obstacle != null) { obstacleStart = obstacle.position; obstacleStartScale = obstacle.localScale; }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame) SetHuman(new Vector3(0f, 0f, -4f), Quaternion.identity);
        if (keyboard.f2Key.wasPressedThisFrame) SetHuman(new Vector3(-6f, 0f, -4f), Quaternion.Euler(0f, 270f, 0f));
        if (keyboard.f3Key.wasPressedThisFrame) SetHuman(new Vector3(0f, 0f, -13f), Quaternion.Euler(0f, 180f, 0f));
        if (keyboard.f4Key.wasPressedThisFrame) turningToHuman = true;
        if (keyboard.f5Key.wasPressedThisFrame) PlaceOccluder();
        if (keyboard.f6Key.wasPressedThisFrame) ResetScenario();
        if (keyboard.f7Key.wasPressedThisFrame && humanAnimator != null)
        {
            humanAnimator.Play("Idle", 0, 0f);
            if (humanGestures != null) humanGestures.SetGesture(HumanTargetGesture.Idle);
        }
        if (keyboard.f8Key.wasPressedThisFrame && humanGestures != null)
            humanGestures.SetGesture(HumanTargetGesture.PointLeft, targetObjectA);
        if (keyboard.f9Key.wasPressedThisFrame && humanGestures != null)
            humanGestures.SetGesture(HumanTargetGesture.PointRight, targetObjectB);

        if (turningToHuman && humanTarget != null)
        {
            Vector3 direction = humanTarget.position - robotObserver.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                robotObserver.rotation = Quaternion.RotateTowards(robotObserver.rotation, Quaternion.LookRotation(direction), 75f * Time.deltaTime);
            if (Quaternion.Angle(robotObserver.rotation, Quaternion.LookRotation(direction)) < 1f)
                turningToHuman = false;
        }
    }

    private void SetHuman(Vector3 position, Quaternion rotation)
    {
        turningToHuman = false;
        humanTarget.SetPositionAndRotation(position, rotation);
    }

    private void PlaceOccluder()
    {
        if (obstacle == null || humanTarget == null) return;
        Vector3 midpoint = (robotObserver.position + humanTarget.position) * 0.5f;
        obstacle.position = new Vector3(midpoint.x, 1.5f, midpoint.z);
        obstacle.localScale = new Vector3(obstacleStartScale.x, 3f, obstacleStartScale.z);
    }

    private void ResetScenario()
    {
        turningToHuman = false;
        robotObserver.SetPositionAndRotation(robotStart, robotStartRotation);
        humanTarget.SetPositionAndRotation(humanStart, humanStartRotation);
        if (obstacle != null)
        {
            obstacle.position = obstacleStart;
            obstacle.localScale = obstacleStartScale;
        }
    }
}
