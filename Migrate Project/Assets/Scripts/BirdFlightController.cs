using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BirdFlightController : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float flapForce = 8f;
    [SerializeField] private float maxForwardSpeed = 15f;
    [SerializeField] private float minForwardSpeed = 5f;
    [SerializeField] private float turnSpeed = 50f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float glideDrag = 0.5f;

    [Header("Wing Flapping")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;
    [SerializeField] private float wingFlapAngle = 30f;
    [SerializeField] private float wingFlapSpeed = 10f;

    [Header("Area Constraints")]
    [SerializeField] private float areaLength = 1000f; // Length along X axis
    [SerializeField] private float areaWidth = 200f;   // Width along Z axis
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 200f;

    [Header("VR Controllers")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private float controllerDeadzone = 0.1f;

    private Rigidbody rb;
    private Vector3 previousLeftControllerPos;
    private Vector3 previousRightControllerPos;
    private float currentSpeed;
    private bool isFlapping = false;
    private float wingFlapTimer = 0f;
    private static BirdFlightController _instance;

    private static BirdFlightController Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("PlayerController instance is null. Ensure it exists in scene.");
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        } 
        else if (_instance != this) 
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = glideDrag;
        currentSpeed = minForwardSpeed;

        // Store initial controller positions
        if (leftController != null)
            previousLeftControllerPos = leftController.position;
        if (rightController != null)
            previousRightControllerPos = rightController.position;
    }

    void FixedUpdate()
    {
        HandleFlight();
        ApplyAreaConstraints();
    }

    void Update()
    {
        HandleControllerInput();
        AnimateWings();
    }

    void HandleControllerInput()
    {
        if (leftController == null || rightController == null) return;

        // Get controller velocities
        Vector3 leftVelocity = (leftController.position - previousLeftControllerPos) / Time.deltaTime;
        Vector3 rightVelocity = (rightController.position - previousRightControllerPos) / Time.deltaTime;

        // Flapping detection - quick upward motion
        float leftFlapSpeed = leftVelocity.y;
        float rightFlapSpeed = rightVelocity.y;

        if ((leftFlapSpeed > 2f || rightFlapSpeed > 2f) && !isFlapping)
        {
            FlapWings();
        }

        // Controller orientation for flight control
        Vector3 leftControllerRotation = leftController.localEulerAngles;
        Vector3 rightControllerRotation = rightController.localEulerAngles;

        // Use controller tilt for flight control
        float pitchInput = Mathf.Clamp(NormalizeAngle(rightControllerRotation.x), -1f, 1f);
        float rollInput = Mathf.Clamp(NormalizeAngle(rightControllerRotation.z), -1f, 1f);
        float yawInput = Mathf.Clamp(NormalizeAngle(leftControllerRotation.y), -1f, 1f);

        // Apply deadzone
        if (Mathf.Abs(pitchInput) < controllerDeadzone) pitchInput = 0;
        if (Mathf.Abs(rollInput) < controllerDeadzone) rollInput = 0;
        if (Mathf.Abs(yawInput) < controllerDeadzone) yawInput = 0;

        // Apply rotation based on controller inputs
        float pitch = pitchInput * turnSpeed * Time.deltaTime;
        float yaw = yawInput * turnSpeed * Time.deltaTime;
        float roll = -rollInput * turnSpeed * Time.deltaTime; // Negative for intuitive control

        transform.Rotate(pitch, yaw, roll, Space.Self);

        // Update previous positions
        previousLeftControllerPos = leftController.position;
        previousRightControllerPos = rightController.position;
    }

    void HandleFlight()
    {
        // Forward thrust based on current speed
        Vector3 forwardThrust = transform.forward * currentSpeed;

        // Apply gravity effect (reduced when flapping)
        float currentGravity = isFlapping ? gravity * 0.3f : gravity;
        Vector3 gravityForce = Vector3.up * currentGravity;

        // Combine forces
        Vector3 totalForce = forwardThrust + gravityForce;

        // Apply to rigidbody while preserving some existing velocity
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, totalForce, Time.deltaTime);

        // Speed management
        float verticalInput = Mathf.Clamp(transform.forward.y, -0.5f, 0.5f);
        currentSpeed = Mathf.Lerp(currentSpeed,
            minForwardSpeed + (maxForwardSpeed - minForwardSpeed) * (1 - Mathf.Abs(verticalInput)),
            Time.deltaTime);
    }

    void FlapWings()
    {
        StartCoroutine(FlapCoroutine());

        // Add upward force when flapping
        rb.AddForce(Vector3.up * flapForce, ForceMode.Impulse);

        // Reset vertical velocity slightly to make flapping more effective
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 2f);
        rb.linearVelocity = velocity;
    }

    IEnumerator FlapCoroutine()
    {
        isFlapping = true;

        // Quick upward flap
        float elapsed = 0;
        float flapDuration = 0.2f;

        while (elapsed < flapDuration)
        {
            float t = elapsed / flapDuration;
            float angle = Mathf.Sin(t * Mathf.PI) * wingFlapAngle;

            if (leftWing != null)
                leftWing.localRotation = Quaternion.Euler(angle, 0, 0);
            if (rightWing != null)
                rightWing.localRotation = Quaternion.Euler(-angle, 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Return to neutral
        elapsed = 0;
        while (elapsed < flapDuration)
        {
            float t = elapsed / flapDuration;

            if (leftWing != null)
                leftWing.localRotation = Quaternion.Euler(Mathf.Lerp(wingFlapAngle, 0, t), 0, 0);
            if (rightWing != null)
                rightWing.localRotation = Quaternion.Euler(Mathf.Lerp(-wingFlapAngle, 0, t), 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        isFlapping = false;
    }

    void AnimateWings()
    {
        // Gentle wing animation when gliding
        if (!isFlapping && leftWing != null && rightWing != null)
        {
            float glideAngle = Mathf.Sin(Time.time * 2f) * 5f;
            leftWing.localRotation = Quaternion.Euler(glideAngle, 0, 0);
            rightWing.localRotation = Quaternion.Euler(-glideAngle, 0, 0);
        }
    }

    void ApplyAreaConstraints()
    {
        Vector3 pos = transform.position;

        // X-axis constraint (long strip)
        if (Mathf.Abs(pos.x) > areaLength / 2)
        {
            pos.x = Mathf.Sign(pos.x) * areaLength / 2;

            // Bounce or turn around
            Vector3 newForward = transform.forward;
            newForward.x *= -0.5f;
            transform.forward = Vector3.Slerp(transform.forward, newForward.normalized, Time.deltaTime);
        }

        // Z-axis constraint (width)
        if (Mathf.Abs(pos.z) > areaWidth / 2)
        {
            pos.z = Mathf.Sign(pos.z) * areaWidth / 2;

            // Push bird back toward center
            Vector3 newForward = transform.forward;
            newForward.z *= -0.3f;
            transform.forward = Vector3.Slerp(transform.forward, newForward.normalized, Time.deltaTime);
        }

        // Height constraints
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);

        transform.position = pos;
    }

    float NormalizeAngle(float angle)
    {
        angle = angle % 360;
        if (angle > 180) angle -= 360;
        return angle / 180f;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize the flight area in editor
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        center.y = minHeight + (maxHeight - minHeight) / 2;
        Vector3 size = new Vector3(areaLength, maxHeight - minHeight, areaWidth);
        Gizmos.DrawWireCube(center, size);
    }
}