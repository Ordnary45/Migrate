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
    [SerializeField] private float maxFlapDelay = 0.2f; 

    [Header("Area Constraints")]
    [SerializeField] private float areaLength = 1000f; // Length along X axis
    [SerializeField] private float areaWidth = 200f;   // Width along Z axis
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 200f;

    [Header("VR Controllers")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private float controllerDeadzone = 0.1f;

    [Header("References")]
    [SerializeField] private BirdCollisionHandler collisionHandler;

    [Header("Keyboard Testing")]
    [SerializeField] private bool keyControls = false;

    private Rigidbody rb;
    private Vector3 previousLeftControllerPos;
    private Vector3 previousRightControllerPos;

    private float currentSpeed;
    private bool isFlapping = false;
    private bool leftWingFlapped = false;
    private bool rightWingFlapped = false;
    private float leftFlapTime = 0f;
    private float rightFlapTime = 0f;
    float pitchInput, rollInput, yawInput;

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

    // For handling movement physics and boundary detection
    void FixedUpdate()
    {
        HandleFlight();
        ApplyAreaConstraints();
    }

    // Reads player input and animates the wings visually
    void Update()
    {
        if (keyControls)
        {
            HandleKeyboardInput();
        }
        else
        {
            HandleControllerInput();
        }
        AnimateWings();
    }

    void HandleControllerInput()
    {
        if (leftController == null || rightController == null) return;

        // Get controller velocities
        Vector3 leftVelocity = (leftController.position - previousLeftControllerPos) / Time.deltaTime;
        Vector3 rightVelocity = (rightController.position - previousRightControllerPos) / Time.deltaTime;

        // Flapping detection - checks only for y movement
        float leftFlapSpeed = leftVelocity.y;
        float rightFlapSpeed = rightVelocity.y;

        if ((leftFlapSpeed > 2f) && !isFlapping)
        {
            leftWingFlapped = true;
            leftFlapTime = Time.deltaTime;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();

                    // Reset flap states
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }
        }

        if ((rightFlapSpeed > 2f) && !isFlapping)
        {
            rightWingFlapped = true;
            rightFlapTime = Time.deltaTime;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();

                    // Reset flap states
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }
        }

        // Controller orientation for flight control
        Vector3 leftControllerRotation = leftController.localEulerAngles;
        Vector3 rightControllerRotation = rightController.localEulerAngles;
        Vector3 averagedRotation = (leftControllerRotation + rightControllerRotation) / 2f;

        // Use controller tilt for flight control
        pitchInput = Mathf.Clamp(NormalizeAngle(averagedRotation.x), -1f, 1f);
        rollInput = Mathf.Clamp(NormalizeAngle(averagedRotation.z), -1f, 1f);
        yawInput = Mathf.Clamp(NormalizeAngle(averagedRotation.y), -1f, 1f);

        // Apply deadzone - small rotations are ignored
        if (Mathf.Abs(pitchInput) < controllerDeadzone) pitchInput = 0;
        if (Mathf.Abs(rollInput) < controllerDeadzone) rollInput = 0;
        if (Mathf.Abs(yawInput) < controllerDeadzone) yawInput = 0;

        // Apply rotation based on controller inputs
        float pitch = pitchInput * turnSpeed * Time.deltaTime;
        float roll = rollInput * turnSpeed * Time.deltaTime;    // rollInput Might need to be negative
        float yaw = yawInput * turnSpeed * Time.deltaTime;

        transform.Rotate(pitch, yaw, roll, Space.Self);

        // Update previous positions
        previousLeftControllerPos = leftController.position;
        previousRightControllerPos = rightController.position;
    }

    float NormalizeAngle(float angle)
    {
        angle = angle % 360;
        if (angle > 180) angle -= 360;
        return angle / 180f;
    }

    void HandleKeyboardInput()
    {
        //Flapping is tap based - have to press both within time limit to activate
        if ((Input.GetKeyDown(KeyCode.LeftArrow) && !isFlapping))
        {
            leftWingFlapped = true;
            leftFlapTime = Time.deltaTime;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();

                    // Reset flap states
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }
        }

        if ((Input.GetKeyDown(KeyCode.RightArrow) && !isFlapping))
        {
            rightWingFlapped = true;
            rightFlapTime = Time.deltaTime;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();

                    // Reset flap states
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }
        }            

        //Rotation inputs
        pitchInput = 0f;
        yawInput = 0f;
        rollInput = 0f;

        // Pitch (W/S)
        if (Input.GetKey(KeyCode.W)) pitchInput = -1f;
        if (Input.GetKey(KeyCode.S)) pitchInput = 1f;

        // Yaw (A/D)
        if (Input.GetKey(KeyCode.A)) yawInput = -1f;
        if (Input.GetKey(KeyCode.D)) yawInput = 1f;

        // Roll (Q/E)
        if (Input.GetKey(KeyCode.Q)) rollInput = 1f;
        if (Input.GetKey(KeyCode.E)) rollInput = -1f;

        // Apply rotation
        float pitch = pitchInput * turnSpeed * Time.deltaTime;
        float yaw = yawInput * turnSpeed * Time.deltaTime;
        float roll = rollInput * turnSpeed * Time.deltaTime;

        //Debug.Log(pitchInput + " " + rollInput + " " + yawInput);

        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    // Handles the forward motion and gravity of player
    void HandleFlight()
    {
        // Forward thrust based on current speed towards the direction it faces
        Vector3 forwardThrust = transform.forward * currentSpeed;

        // Apply gravity effect (reduced when flapping - feels more intuitive)
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
    
    // The flapping wings mechanic
    void FlapWings()
    {
        // Animates the wings
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

    // Invisible boundary to restrict player from leaving
    void ApplyAreaConstraints()
    {
        Vector3 pos = transform.position;

        // X-axis constraint (long strip)
        if (pos.x > areaLength)
        {
            pos.x = areaLength;

            // Bounce or turn around
            Vector3 newForward = transform.forward;
            newForward.x *= -0.5f;
            transform.forward = Vector3.Slerp(transform.forward, newForward.normalized, Time.deltaTime);
        }
        else if (pos.x < 0) // Behind the start
        {
            pos.x = 0;

            // Push forward again
            Vector3 newForward = transform.forward;
            newForward.x = Mathf.Abs(newForward.x);
            transform.forward = Vector3.Slerp(transform.forward, newForward.normalized, Time.deltaTime * 2f);
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

    void OnDrawGizmosSelected()
    {
        // Visualize the flight area in editor
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(areaLength / 2, (minHeight + maxHeight) / 2, 0);
        Vector3 size = new Vector3(areaLength, maxHeight - minHeight, areaWidth);
        Gizmos.DrawWireCube(center, size);
    }

    void OnGUI()
    {
        // Display controller values on screen for debugging
        if (leftController != null && rightController != null)
        {
            GUILayout.Label($"Left Controller: {leftController.localEulerAngles}");
            GUILayout.Label($"Right Controller: {rightController.localEulerAngles}");
            GUILayout.Label($"Averaged Input - Pitch: {pitchInput}, Roll: {rollInput}, Yaw: {yawInput}");
            GUILayout.Label($"Current Speed: {currentSpeed:F1}");
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Health: {collisionHandler.GetCurrentHealth()}");
        }
    }
}