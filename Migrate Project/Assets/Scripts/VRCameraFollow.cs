using UnityEngine;

public class VRCameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target; // The bird
    [SerializeField] private float positionSmoothing = 5f;
    [SerializeField] private Vector3 positionOffset = new Vector3(0, 0f, 0f);

    [Header("Camera Settings")]
    [SerializeField] private bool maintainWorldOffset = true; // Keep offset in world space, not relative to bird rotation
    [SerializeField] private float maxFollowDistance = 50f; // Prevent camera from going too far

    [Header("Motion Sickness Reduction")]
    [SerializeField] private bool smoothPosition = true;

    private Vector3 targetPosition;
    private Vector3 velocityRef = Vector3.zero;
    private Quaternion initialCameraRotation;

    void Start()
    {
        if (target == null)
        {
            // Try to find the bird automatically
            GameObject bird = GameObject.FindGameObjectWithTag("Player");
            if (bird != null)
                target = bird.transform;
            else
                Debug.LogError("No target assigned to VRCameraFollow!");
        }

        initialCameraRotation = transform.rotation;
        if (target != null)
        {
            transform.position = target.position + positionOffset;
        }
    }

    // Camera rotation remains independent - stays level with horizon
    // This prevents motion sickness by not following bird's rotation
    void LateUpdate()
    {
        if (target == null) return;

        // Calculate target position in WORLD space, not relative to bird rotation
        if (maintainWorldOffset)
        {
            targetPosition = target.position + positionOffset;
        }
        else
        {
            targetPosition = target.position + target.TransformDirection(positionOffset);
        }

        // Clamp the distance to prevent camera from going too far
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget > maxFollowDistance)
        {
            targetPosition = transform.position + (targetPosition - transform.position).normalized * maxFollowDistance;
        }

        // Apply position smoothing
        if (smoothPosition)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocityRef, 1f / positionSmoothing);
        }
        else
        {
            transform.position = targetPosition;
        }
        transform.rotation = initialCameraRotation;
    }
}