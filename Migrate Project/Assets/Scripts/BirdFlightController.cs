using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BirdFlightController : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float flapForce = 18f;
    [SerializeField] private float maxForwardSpeed = 25f;
    [SerializeField] private float minForwardSpeed = 12f;
    [SerializeField] private float smoothing = 12f;
    [SerializeField] private float gravity = -8f;
    [SerializeField] private float glideDrag = 0.3f;

    [Header("Wing Flapping")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;
    [SerializeField] private float wingFlapAngle = 35f;
    [SerializeField] private float maxFlapDelay = 0.2f;
    [SerializeField] private float flapVelocityThreshold = 0.6f;
    [SerializeField] private float startupFlapDelay = 2f;
    [SerializeField] private float flapCooldown = 0.5f;
    [SerializeField] private float wingCooldown = 0.3f;

    [Header("Area Constraints")]
    [SerializeField] private float areaLength = 1000f;
    [SerializeField] private float areaWidth = 200f;
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 200f;

    [Header("VR Controllers")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private float controllerDeadzone = 2f;
    [SerializeField] private float controllerSensitivity = 0.8f;

    [Header("Controller Calibration")]
    [SerializeField] private bool calibrateOnStart = true;

    [SerializeField] private float winZPosition = 1000f;
    [SerializeField] private string winSceneName = "You win";
    [SerializeField] private float winDelay = 3f;

    [Header("References")]
    [SerializeField] private Transform birdVisual;
    [SerializeField] private BirdCollisionHandler collisionHandler;

    [Header("Keyboard Testing")]
    [SerializeField] private bool keyControls = false;

    [Header("Movement Speeds")]
    [SerializeField] private float turnSpeedMultiplier = 2f;
    [SerializeField] private float flapBoostMultiplier = 1.5f;

    private Rigidbody rb;
    private Vector3 previousLeftControllerPos;
    private Vector3 previousRightControllerPos;

    private float currentSpeed;
    private bool isFlapping = false;

    // Flap detection variables
    private bool leftWingFlapped = false;
    private bool rightWingFlapped = false;
    private float leftFlapTime = 0f;
    private float rightFlapTime = 0f;
    private float lastFlapTime = 0f;

    // Per-wing cooldown to prevent multiple triggers
    private float leftWingTriggerCooldown = 0f;
    private float rightWingTriggerCooldown = 0f;

    // Track previous flap state for detection
    private bool leftWingWasFlapping = false;
    private bool rightWingWasFlapping = false;

    private bool flapsEnabled = false;
    private float startupTimer = 0f;

    private Quaternion targetRotation;

    // Calibration
    private bool isCalibrated = false;
    private Vector3 calibratedLeftRotation;
    private Vector3 calibratedRightRotation;
    private Quaternion calibratedNeutralRotation;

    // Track initial forward direction
    private Vector3 initialForward;

    // Win condition
    private bool hasWon = false;
    private bool winTriggered = false;

    public float GetAreaLength() => areaLength;
    public float GetMinHeight() => minHeight;
    public float GetMaxHeight() => maxHeight;
    public float GetAreaWidth() => areaWidth;

    private static BirdFlightController _instance;
    private static BirdFlightController Instance
    {
        get
        {
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
        // Get or add a Rigidbody component to handle physics simulation
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        // Starting forward speed, gravity is manually applied later
        rb.useGravity = false;
        rb.linearDamping = glideDrag;
        currentSpeed = minForwardSpeed;

        initialForward = transform.forward;
        targetRotation = transform.rotation;

        // Reset bird visual's local rotation to identity
        if (birdVisual != null)
        {
            birdVisual.localRotation = Quaternion.identity;
        }

        // Store initial controller positions for velocity calculation
        if (leftController != null)
            previousLeftControllerPos = leftController.position;
        if (rightController != null)
            previousRightControllerPos = rightController.position;

        StartCoroutine(EnableFlapsAfterDelay());

        if (calibrateOnStart)
        {
            StartCoroutine(AutoCalibrate());
        }
    }

    void FixedUpdate()
    {
        if (hasWon) return;

        HandleFlight();
        ApplyAreaConstraints();

        if (!keyControls && !isRecoveringFromCollision() && isCalibrated)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing * Time.fixedDeltaTime * turnSpeedMultiplier);
        }

        CheckWinCondition();
    }

    void Update()
    {
        if (hasWon) return;
        CheckCalibrationButton();

        // Update wing cooldowns
        if (leftWingTriggerCooldown > 0) leftWingTriggerCooldown -= Time.deltaTime;
        if (rightWingTriggerCooldown > 0) rightWingTriggerCooldown -= Time.deltaTime;

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

    void CheckCalibrationButton()
    {
        bool aButtonPressed = OVRInput.GetDown(OVRInput.Button.One);
        bool xButtonPressed = OVRInput.GetDown(OVRInput.Button.Three);

        // If either A button (right hand) OR X button (left hand) was pressed
        if (aButtonPressed || xButtonPressed)
        {
            CalibrateNeutralPosition();
        }
    }

    void HandleControllerInput()
    {
        if (leftController == null || rightController == null) return;

        // Calculate velocities
        Vector3 leftVelocity = (leftController.position - previousLeftControllerPos) / Time.deltaTime;
        Vector3 rightVelocity = (rightController.position - previousRightControllerPos) / Time.deltaTime;

        float leftFlapSpeed = leftVelocity.y;
        float rightFlapSpeed = rightVelocity.y;

        // Flap detection with state tracking and cooldowns
        if (flapsEnabled && !isFlapping && Time.time - lastFlapTime > flapCooldown)
        {
            bool leftTriggered = false;
            bool rightTriggered = false;

            // Left wing - detect when speed crosses threshold from below
            bool leftFlappingNow = leftFlapSpeed > flapVelocityThreshold;
            if (leftFlappingNow && !leftWingWasFlapping && !leftWingFlapped && leftWingTriggerCooldown <= 0)
            {
                leftWingFlapped = true;
                leftFlapTime = Time.time;
                leftWingTriggerCooldown = wingCooldown;
                leftTriggered = true;
            }

            // Right wing - detect when speed crosses threshold from below
            bool rightFlappingNow = rightFlapSpeed > flapVelocityThreshold;
            if (rightFlappingNow && !rightWingWasFlapping && !rightWingFlapped && rightWingTriggerCooldown <= 0)
            {
                rightWingFlapped = true;
                rightFlapTime = Time.time;
                rightWingTriggerCooldown = wingCooldown;
                rightTriggered = true;
            }

            // Update previous states for next frame
            leftWingWasFlapping = leftFlapSpeed > flapVelocityThreshold * 0.5f;
            rightWingWasFlapping = rightFlapSpeed > flapVelocityThreshold * 0.5f;

            // If both wings have flapped within the delay window, trigger flap
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();
                    lastFlapTime = Time.time;

                    // Reset flap flags
                    leftWingFlapped = false;
                    rightWingFlapped = false;

                    // Reset controller positions to prevent immediate re-trigger
                    previousLeftControllerPos = leftController.position;
                    previousRightControllerPos = rightController.position;
                }
                else
                {
                    // Wings flapped too far apart - reset individual flags
                    if (Time.time - leftFlapTime > maxFlapDelay)
                        leftWingFlapped = false;
                    if (Time.time - rightFlapTime > maxFlapDelay)
                        rightWingFlapped = false;
                }
            }

            // Auto-reset individual flaps if waiting too long
            if (leftWingFlapped && Time.time - leftFlapTime > maxFlapDelay)
                leftWingFlapped = false;
            if (rightWingFlapped && Time.time - rightFlapTime > maxFlapDelay)
                rightWingFlapped = false;
        }
        else
        {
            // Reset the "was flapping" states when not in flap mode
            leftWingWasFlapping = false;
            rightWingWasFlapping = false;
        }

        // Flight control code
        if (isCalibrated)
        {
            // Gets current controller orientations
            Vector3 leftEuler = leftController.localEulerAngles;
            Vector3 rightEuler = rightController.localEulerAngles;

            Vector3 leftDelta = new Vector3(
                Mathf.DeltaAngle(calibratedLeftRotation.x, leftEuler.x),
                Mathf.DeltaAngle(calibratedLeftRotation.y, leftEuler.y),
                Mathf.DeltaAngle(calibratedLeftRotation.z, leftEuler.z)
            );
            Vector3 rightDelta = new Vector3(
                Mathf.DeltaAngle(calibratedRightRotation.x, rightEuler.x),
                Mathf.DeltaAngle(calibratedRightRotation.y, rightEuler.y),
                Mathf.DeltaAngle(calibratedRightRotation.z, rightEuler.z)
            );

            // Average both controllers for smoother, more stable control
            Vector3 averagedDelta = (leftDelta + rightDelta) / 2f;

            // X-axis (pitch) - tilting forward/backward controls nose up/down
            // Y-axis (yaw) - twisting controls left/right turning
            // Z-axis (roll) - tilting sideways controls banking
            float pitchInput = averagedDelta.x * controllerSensitivity * 1.5f;
            float yawInput = averagedDelta.y * controllerSensitivity;
            float rollInput = averagedDelta.z * controllerSensitivity;

            // Apply deadzone - ignore small movements
            if (Mathf.Abs(pitchInput) < controllerDeadzone) pitchInput = 0;
            if (Mathf.Abs(yawInput) < controllerDeadzone) yawInput = 0;
            if (Mathf.Abs(rollInput) < controllerDeadzone) rollInput = 0;

            // Clamp inputs to prevent extreme rotations
            pitchInput = Mathf.Clamp(pitchInput, -60f, 60f);
            yawInput = Mathf.Clamp(yawInput, -80f, 80f);
            rollInput = Mathf.Clamp(rollInput, -80f, 80f);

            Quaternion desiredRotation = Quaternion.Euler(pitchInput, yawInput, rollInput);
            targetRotation = calibratedNeutralRotation * desiredRotation;

            // Add turning force based on roll angle (banking turns)
            float turnStrength = -rollInput * 0.8f;
            Vector3 turnForce = transform.right * turnStrength * turnSpeedMultiplier;
            rb.AddForce(turnForce, ForceMode.Acceleration);
        }
        previousLeftControllerPos = leftController.position;
        previousRightControllerPos = rightController.position;
    }

    void HandleKeyboardInput()
    {
        // Keyboard flap detection
        if (!isFlapping && Time.time - lastFlapTime > flapCooldown)
        {
            bool leftPressed = Input.GetKeyDown(KeyCode.LeftArrow);
            bool rightPressed = Input.GetKeyDown(KeyCode.RightArrow);

            if (leftPressed && rightPressed)
            {
                FlapWings();
                lastFlapTime = Time.time;
            }
        }

        float pitchInput = 0f;
        float yawInput = 0f;
        float rollInput = 0f;

        if (Input.GetKey(KeyCode.W)) pitchInput = 1f;
        if (Input.GetKey(KeyCode.S)) pitchInput = -1f;
        if (Input.GetKey(KeyCode.A)) yawInput = -1f;
        if (Input.GetKey(KeyCode.D)) yawInput = 1f;
        if (Input.GetKey(KeyCode.Q)) rollInput = -1f;
        if (Input.GetKey(KeyCode.E)) rollInput = 1f;

        float turnSpeed = 120f;
        float pitch = pitchInput * turnSpeed * Time.deltaTime;
        float yaw = yawInput * turnSpeed * Time.deltaTime;
        float roll = rollInput * turnSpeed * Time.deltaTime;

        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    void HandleFlight()
    {
        // Get current flight angles
        float pitchAngle = transform.forward.y;
        float rollAngle = transform.right.y;

        // Speed affected by pitch
        float pitchSpeedModifier = 1f - (pitchAngle * 0.7f);
        pitchSpeedModifier = Mathf.Clamp(pitchSpeedModifier, 0.4f, 1.5f);

        float rollDrag = Mathf.Abs(rollAngle) * 0.2f;

        // Target speed based on pitch and roll
        float targetSpeed = Mathf.Lerp(minForwardSpeed, maxForwardSpeed,
            (1f - Mathf.Abs(pitchAngle)) * (1f - rollDrag));

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 4f);

        // Forward thrust based on current speed
        Vector3 forwardThrust = transform.forward * currentSpeed;

        float speedLift = 6f * (currentSpeed / maxForwardSpeed);
        float angleOfAttack = pitchAngle * 2f;
        float aoaLift = angleOfAttack * 4f;

        // Stall mechanics - Nose up at low speed losses lift
        float stallThreshold = 0.6f;
        float stallMultiplier = 1f;
        if (pitchAngle > stallThreshold && currentSpeed < minForwardSpeed * 1.2f)
        {
            stallMultiplier = Mathf.Lerp(1f, 0.2f, (pitchAngle - stallThreshold) / 0.4f);
        }

        // Combine lift sources and add gravity
        float totalLift = (speedLift + aoaLift) * stallMultiplier;
        float verticalForce = totalLift + gravity;

        if (isFlapping)
        {
            verticalForce += 12f;
            currentSpeed += Time.deltaTime * 8f;
            currentSpeed = Mathf.Min(currentSpeed, maxForwardSpeed);
        }

        // Apply forces to Rigidbody
        Vector3 totalForce = forwardThrust + (Vector3.up * verticalForce);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, totalForce, Time.deltaTime * 1.5f);

        float aoaDrag = Mathf.Abs(pitchAngle) * 1.2f;
        rb.linearDamping = glideDrag + aoaDrag;

        // To revent unrealistic falling/climbing speeds
        float maxFallSpeed = -14f;
        float maxClimbSpeed = 20f;

        if (rb.linearVelocity.y < maxFallSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxFallSpeed, rb.linearVelocity.z);
        else if (rb.linearVelocity.y > maxClimbSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxClimbSpeed, rb.linearVelocity.z);
    }

    void FlapWings()
    {
        // Prevent multiple flaps while already flapping
        if (isFlapping) return;

        // Start wing animation
        StartCoroutine(FlapCoroutine());

        // Less effective flaps when pitching up
        float pitchFactor = 1f - Mathf.Abs(transform.forward.y) * 0.4f;
        // More effective flaps at higher speeds
        float speedFactor = Mathf.Clamp01(currentSpeed / maxForwardSpeed);
        float flapStrength = flapForce * (0.9f + speedFactor * 0.5f) * pitchFactor * flapBoostMultiplier;

        // Apply upward and forward force for the flap
        rb.AddForce(Vector3.up * flapStrength, ForceMode.Impulse);
        rb.AddForce(transform.forward * flapStrength * 0.5f, ForceMode.Impulse);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 3f);
        rb.linearVelocity = velocity;

        // Speed boost from flapping
        currentSpeed += 2f;
        currentSpeed = Mathf.Min(currentSpeed, maxForwardSpeed);
    }

    IEnumerator FlapCoroutine()
    {
        if (isFlapping) yield break;
        isFlapping = true;

        float elapsed = 0;
        float flapDuration = 0.15f;

        // Store original rotations to prevent teleportation
        Quaternion leftOriginal = leftWing != null ? leftWing.localRotation : Quaternion.identity;
        Quaternion rightOriginal = rightWing != null ? rightWing.localRotation : Quaternion.identity;

        // Flap down
        while (elapsed < flapDuration)
        {
            float t = elapsed / flapDuration;
            float angle = Mathf.Sin(t * Mathf.PI) * wingFlapAngle;

            if (leftWing != null)
                leftWing.localRotation = leftOriginal * Quaternion.Euler(angle, 0, 0);
            if (rightWing != null)
                rightWing.localRotation = rightOriginal * Quaternion.Euler(-angle, 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Flap up
        elapsed = 0;
        while (elapsed < flapDuration)
        {
            float t = elapsed / flapDuration;
            float angle = Mathf.Lerp(wingFlapAngle, 0, t);

            if (leftWing != null)
                leftWing.localRotation = leftOriginal * Quaternion.Euler(angle, 0, 0);
            if (rightWing != null)
                rightWing.localRotation = rightOriginal * Quaternion.Euler(-angle, 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset to original
        if (leftWing != null)
            leftWing.localRotation = leftOriginal;
        if (rightWing != null)
            rightWing.localRotation = rightOriginal;

        isFlapping = false;
    }

    void AnimateWings()
    {
        if (!isFlapping && leftWing != null && rightWing != null)
        {
            float glideAngle = Mathf.Sin(Time.time * 3f) * 8f;
            leftWing.localRotation = Quaternion.Euler(glideAngle, 0, 0);
            rightWing.localRotation = Quaternion.Euler(-glideAngle, 0, 0);
        }
    }

    IEnumerator AutoCalibrate()
    {
        yield return new WaitForSeconds(1f);
        CalibrateNeutralPosition();
    }

    IEnumerator EnableFlapsAfterDelay()
    {
        flapsEnabled = false;
        startupTimer = 0f;

        while (startupTimer < startupFlapDelay)
        {
            startupTimer += Time.deltaTime;
            yield return null;
        }

        flapsEnabled = true;
        Debug.Log("Flaps enabled!"); // For debugging
    }

    public void CalibrateNeutralPosition()
    {
        if (leftController != null && rightController != null)
        {
            // Gets current controller orientations
            calibratedLeftRotation = leftController.localEulerAngles;
            calibratedRightRotation = rightController.localEulerAngles;
            // Store current bird rotation as the neutral flight orientation
            calibratedNeutralRotation = transform.rotation;
            isCalibrated = true;

            // Reset any pending flap states
            leftWingFlapped = false;
            rightWingFlapped = false;

            if (rb != null)
            {
                rb.angularVelocity = Vector3.zero;
            }

            targetRotation = calibratedNeutralRotation;
        }
    }

    void ApplyAreaConstraints()
    {
        Vector3 pos = transform.position;

        // Z-axis constraint - prevent flying beyond the end of the area or behind the start
        if (pos.z > areaLength)
        {
            pos.z = areaLength;
        }
        else if (pos.z < 0)
        {
            pos.z = 0;
        }

        // X-axis constraint - prevent flying too far left or right
        if (Mathf.Abs(pos.x) > areaWidth / 2)
        {
            pos.x = Mathf.Sign(pos.x) * areaWidth / 2;
        }

        // Y-axis constraint - prevent flying too low or too high
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }

    bool isRecoveringFromCollision()
    {
        if (collisionHandler != null)
        {
            return collisionHandler.IsRecovering;
        }
        return false;
    }

    void CheckWinCondition()
    {
        if (!hasWon && !winTriggered && transform.position.z >= winZPosition)
        {
            TriggerWin();
        }
    }

    void TriggerWin()
    {
        winTriggered = true;
        hasWon = true;

        // Give the bird a final forward boost
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * 20;
        }

        // Disable flapping systems to prevent further input
        StopAllCoroutines();
        flapsEnabled = false;
        isFlapping = false;

        StartCoroutine(LoadWinSceneWithDelay());
    }

    IEnumerator LoadWinSceneWithDelay()
    {
        yield return new WaitForSeconds(winDelay);
        SceneManager.LoadScene(winSceneName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(0, (minHeight + maxHeight) / 2, areaLength / 2);
        Vector3 size = new Vector3(areaWidth, maxHeight - minHeight, areaLength);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.right * 3f);
    }

    void OnGUI()
    {
        GUILayout.Label("=== BIRD FLIGHT CONTROLLER ===");
        GUILayout.Label($"Position: {transform.position:F1}");
        GUILayout.Label($"Speed: {currentSpeed:F1}");

        if (leftController != null && rightController != null)
        {
            if (isCalibrated)
            {
                GUILayout.Label("Calibrated");
            }
            else
            {
                GUILayout.Label("Not Calibrated - Press A/X button");
            }
        }

        if (collisionHandler != null)
        {
            GUILayout.Label($"Health: {collisionHandler.GetCurrentHealth()}/{collisionHandler.GetMaxHealth()}");
        }
    }

    public void ResetFlapStates()
    {
        isFlapping = false;
        leftWingFlapped = false;
        rightWingFlapped = false;
        leftFlapTime = 0f;
        rightFlapTime = 0f;
        leftWingWasFlapping = false;
        rightWingWasFlapping = false;
        leftWingTriggerCooldown = 0f;
        rightWingTriggerCooldown = 0f;

        StopAllCoroutines();

        if (leftWing != null)
            leftWing.localRotation = Quaternion.identity;
        if (rightWing != null)
            rightWing.localRotation = Quaternion.identity;

        if (leftController != null)
            previousLeftControllerPos = leftController.position;
        if (rightController != null)
            previousRightControllerPos = rightController.position;
    }
}