using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThirdPersonFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.8f, -5.5f);
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.2f;
    [SerializeField, Min(0.01f)] private float rotationSpeed = 8f;
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.25f, 0f);

    private Vector3 positionVelocity;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.TransformPoint(localOffset);
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

        Vector3 lookDirection = target.position + lookOffset - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
