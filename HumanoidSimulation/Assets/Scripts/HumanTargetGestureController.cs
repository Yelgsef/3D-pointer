using UnityEngine;

public enum HumanTargetGesture
{
    Idle,
    PointLeft,
    PointRight
}

[DisallowMultipleComponent]
public sealed class HumanTargetGestureController : MonoBehaviour
{
    private struct BonePose
    {
        public Transform transform;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 3f;
    [SerializeField, Range(0.5f, 0.95f)] private float reachFraction = 0.9f;

    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private Transform leftIndex;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;
    private Transform rightIndex;
    private BonePose[] bindPose;
    private float leftBlend;
    private float rightBlend;

    public HumanTargetGesture CurrentGesture { get; private set; }
    public Transform CurrentTarget { get; private set; }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("HumanTarget gestures require a valid Humanoid Avatar on the Animator.", this);
            enabled = false;
            return;
        }

        leftUpperArm = GetBone(HumanBodyBones.LeftUpperArm);
        leftLowerArm = GetBone(HumanBodyBones.LeftLowerArm);
        leftHand = GetBone(HumanBodyBones.LeftHand);
        leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        rightUpperArm = GetBone(HumanBodyBones.RightUpperArm);
        rightLowerArm = GetBone(HumanBodyBones.RightLowerArm);
        rightHand = GetBone(HumanBodyBones.RightHand);
        rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        if (leftUpperArm == null || leftLowerArm == null || leftHand == null ||
            rightUpperArm == null || rightLowerArm == null || rightHand == null)
        {
            enabled = false;
            return;
        }

        SkinnedMeshRenderer renderer = animator.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null || renderer.rootBone == null)
        {
            Debug.LogError("The HumanTarget skinned mesh has no root bone.", this);
            enabled = false;
            return;
        }

        Transform[] rigTransforms = renderer.rootBone.GetComponentsInChildren<Transform>(true);
        bindPose = new BonePose[rigTransforms.Length];
        for (int i = 0; i < rigTransforms.Length; i++)
        {
            bindPose[i] = new BonePose
            {
                transform = rigTransforms[i],
                position = rigTransforms[i].localPosition,
                rotation = rigTransforms[i].localRotation,
                scale = rigTransforms[i].localScale
            };
        }

        CurrentGesture = HumanTargetGesture.Idle;
    }

    public void SetGesture(HumanTargetGesture gesture, Transform target = null)
    {
        CurrentGesture = gesture;
        CurrentTarget = gesture == HumanTargetGesture.Idle ? null : target;
    }

    private void Update()
    {
        float targetLeft = CurrentGesture == HumanTargetGesture.PointLeft && CurrentTarget != null ? 1f : 0f;
        float targetRight = CurrentGesture == HumanTargetGesture.PointRight && CurrentTarget != null ? 1f : 0f;
        leftBlend = Mathf.MoveTowards(leftBlend, targetLeft, blendSpeed * Time.deltaTime);
        rightBlend = Mathf.MoveTowards(rightBlend, targetRight, blendSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (animator == null || !animator.isHuman || CurrentTarget == null)
        {
            RestoreBindPose();
            return;
        }

        RestoreBindPose();

        // Animator evaluates first; restore the imported pose before driving one arm procedurally.
        if (leftBlend > 0f)
            AimArm(leftUpperArm, leftLowerArm, leftHand, leftIndex, leftBlend, 1f);
        if (rightBlend > 0f)
            AimArm(rightUpperArm, rightLowerArm, rightHand, rightIndex, rightBlend, -1f);
    }

    private void RestoreBindPose()
    {
        if (bindPose == null)
            return;

        for (int i = 0; i < bindPose.Length; i++)
        {
            BonePose pose = bindPose[i];
            pose.transform.localPosition = pose.position;
            pose.transform.localRotation = pose.rotation;
            pose.transform.localScale = pose.scale;
        }
    }

    private void AimArm(
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Transform index,
        float weight,
        float side)
    {
        if (upperArm == null || lowerArm == null || hand == null)
            return;

        Vector3 shoulder = upperArm.position;
        Vector3 targetOffset = CurrentTarget.position - shoulder;
        if (targetOffset.sqrMagnitude < 0.0001f)
            return;

        Vector3 direction = targetOffset.normalized;
        Vector3 elbow = lowerArm.position;
        float upperLength = Vector3.Distance(shoulder, elbow);
        float lowerLength = Vector3.Distance(elbow, hand.position);
        float minReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        float maxReach = (upperLength + lowerLength) * reachFraction;
        float reach = Mathf.Clamp(targetOffset.magnitude, minReach, maxReach);
        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) /
                      (2f * reach);
        float elbowHeight = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

        Vector3 bend = Vector3.ProjectOnPlane(-transform.up, direction);
        if (bend.sqrMagnitude < 0.001f)
            bend = Vector3.ProjectOnPlane(transform.right, direction);
        bend = (bend.normalized + transform.right * side * 0.25f).normalized;
        Vector3 elbowTarget = shoulder + direction * along + bend * elbowHeight;
        Vector3 handTarget = shoulder + direction * reach;

        Quaternion upperAim = Quaternion.FromToRotation(elbow - shoulder, elbowTarget - shoulder);
        upperArm.rotation = Quaternion.Slerp(upperArm.rotation, upperAim * upperArm.rotation, weight);

        Vector3 currentHandDirection = hand.position - lowerArm.position;
        Vector3 targetHandDirection = handTarget - lowerArm.position;
        if (currentHandDirection.sqrMagnitude > 0.0001f && targetHandDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion lowerAim = Quaternion.FromToRotation(currentHandDirection, targetHandDirection);
            lowerArm.rotation = Quaternion.Slerp(lowerArm.rotation, lowerAim * lowerArm.rotation, weight);
        }

        if (index != null)
        {
            Vector3 fingerDirection = index.position - hand.position;
            if (fingerDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion handAim = Quaternion.FromToRotation(fingerDirection, direction);
                hand.rotation = Quaternion.Slerp(hand.rotation, handAim * hand.rotation, weight);
            }
        }
    }

    private Transform GetBone(HumanBodyBones bone)
    {
        Transform result = animator.GetBoneTransform(bone);
        if (result == null)
            Debug.LogError("The HumanTarget Avatar is missing the " + bone + " bone.", this);
        return result;
    }
}
