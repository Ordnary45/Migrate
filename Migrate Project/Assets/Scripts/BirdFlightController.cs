using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BirdFlightController : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float flapForce = 14f;
    [SerializeField] private float maxForwardSpeed = 18f;
    [SerializeField] private float minForwardSpeed = 8f;
    [SerializeField] private float smoothing = 8f;
    [SerializeField] private float gravity = -6f;
    [SerializeField] private float glideDrag = 0.4f;

    [Header("Wing Flapping")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;
    [SerializeField] private float wingFlapAngle = 30f;
    [SerializeField] private float maxFlapDelay = 0.2f;
    [SerializeField] private float flapVelocityThreshold = 0.8f;
    [SerializeField] private float startupFlapDelay = 2f;

    [Header("Area Constraints")]
    [SerializeField] private float areaLength = 1000f;
    [SerializeField] private float areaWidth = 200f;
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 200f;

    [Header("VR Controllers")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private float controllerDeadzone = 4f;
    [SerializeField] private float controllerSensitivity = 0.4f;

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

    private Rigidbody rb;
    private Vector3 previousLeftControllerPos;
    private Vector3 previousRightControllerPos;

    private float currentSpeed;
    private bool isFlapping = false;
    private bool leftWingFlapped = false;
    private bool rightWingFlapped = false;
    private float leftFlapTime = 0f;
    private float rightFlapTime = 0f;

    private bool flapsEnabled = false;
    private float startupTimer = 0f;
    private float lastFlapTime = 0f;
    private float flapCooldown = 1f;

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
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = glideDrag;
        currentSpeed = minForwardSpeed;

        // Store the initial forward direction from the scene
        initialForward = transform.forward;

        // Set target rotation to match current orientation
        targetRotation = transform.rotation;

        // Handle bird visual - don't override its local rotation
        if (birdVisual != null)
        {
            birdVisual.localRotation = Quaternion.identity;
        }

        // Store initial controller positions
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

        // Only apply rotation if calibrated and not recovering
        if (!keyControls && !isRecoveringFromCollision() && isCalibrated)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing * Time.fixedDeltaTime);
        }

        // Check win condition based on Z position
        CheckWinCondition();
    }

    void Update()
    {
        if (hasWon) return;
        CheckCalibrationButton();

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

        if (aButtonPressed || xButtonPressed)
        {
            CalibrateNeutralPosition();
        }
    }

    void HandleControllerInput()
    {
        if (leftController == null || rightController == null) return;

        Vector3 leftVelocity = (leftController.position - previousLeftControllerPos) / Time.deltaTime;
        Vector3 rightVelocity = (rightController.position - previousRightControllerPos) / Time.deltaTime;

        float leftFlapSpeed = leftVelocity.y;
        float rightFlapSpeed = rightVelocity.y;

        // Flap Detection Logic:
        if (flapsEnabled && !isFlapping && Time.time - lastFlapTime > flapCooldown)
        {
            // Check if left wing flapped
            if (leftFlapSpeed > flapVelocityThreshold && !leftWingFlapped)
            {
                leftWingFlapped = true;
                leftFlapTime = Time.time;
            }

            // Check if right wing flapped
            if (rightFlapSpeed > flapVelocityThreshold && !rightWingFlapped)
            {
                rightWingFlapped = true;
                rightFlapTime = Time.time;
            }

            // If both wings have flapped within the delay window, trigger flap
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();
                    lastFlapTime = Time.time;

                    // Reset the flap flags
                    leftWingFlapped = false;
                    rightWingFlapped = false;

                    // Reset controller positions to prevent immediate re-trigger
                    previousLeftControllerPos = leftController.position;
                    previousRightControllerPos = rightController.position;
                }
                else
                {
                    // Wings flapped but too far apart in time - reset flags
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }

            // Auto-reset flags if they've been waiting too long
            if (leftWingFlapped && Time.time - leftFlapTime > maxFlapDelay)
                leftWingFlapped = false;
            if (rightWingFlapped && Time.time - rightFlapTime > maxFlapDelay)
                rightWingFlapped = false;
        }

        if (isCalibrated)
        {
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

            Vector3 averagedDelta = (leftDelta + rightDelta) / 2f;

            // Natural flight controls
            float pitchInput = averagedDelta.x * controllerSensitivity;
            float yawInput = averagedDelta.y * controllerSensitivity;
            float rollInput = averagedDelta.z * controllerSensitivity;

            // Apply deadzone
            if (Mathf.Abs(pitchInput) < controllerDeadzone) pitchInput = 0;
            if (Mathf.Abs(yawInput) < controllerDeadzone) yawInput = 0;
            if (Mathf.Abs(rollInput) < controllerDeadzone) rollInput = 0;

            // Apply rotation limits
            pitchInput = Mathf.Clamp(pitchInput, -45f, 45f);
            yawInput = Mathf.Clamp(yawInput, -60f, 60f);
            rollInput = Mathf.Clamp(rollInput, -60f, 60f);

            Quaternion desiredRotation = Quaternion.Euler(pitchInput, yawInput, rollInput);
            targetRotation = calibratedNeutralRotation * desiredRotation;

            // When banking, add a turning force based on roll angle
            float turnStrength = -rollInput * 0.5f;
            Vector3 turnForce = transform.right * turnStrength;
            rb.AddForce(turnForce, ForceMode.Acceleration);

        }

        previousLeftControllerPos = leftController.position;
        previousRightControllerPos = rightController.position;
    }

    void HandleKeyboardInput()
    {
        if ((Input.GetKeyDown(KeyCode.LeftArrow) && !isFlapping))
        {
            leftWingFlapped = true;
            leftFlapTime = Time.time;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
            }
        }

        if ((Input.GetKeyDown(KeyCode.RightArrow) && !isFlapping))
        {
            rightWingFlapped = true;
            rightFlapTime = Time.time;
            if (leftWingFlapped && rightWingFlapped)
            {
                if (Mathf.Abs(leftFlapTime - rightFlapTime) <= maxFlapDelay)
                {
                    FlapWings();
                    leftWingFlapped = false;
                    rightWingFlapped = false;
                }
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

        float turnSpeed = 60f;
        float pitch = pitchInput * turnSpeed * Time.deltaTime;
        float yaw = yawInput * turnSpeed * Time.deltaTime;
        float roll = rollInput * turnSpeed * Time.deltaTime;

        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    void HandleFlight()
    {
        // Get current flight angles
        float pitchAngle = transform.forward.y; // -1 to 1, negative = nose down, positive = nose up
        float rollAngle = transform.right.y; // Bank angle (-1 to 1)
        float yawAngle = transform.forward.x; // Direction relative to forward

        // Speed affected by pitch (nose up = slower, nose down = faster)
        float pitchSpeedModifier = 1f - (pitchAngle * 0.7f); // Nose up reduces speed
        pitchSpeedModifier = Mathf.Clamp(pitchSpeedModifier, 0.5f, 1.3f);

        // Roll also affects speed slightly (banking creates drag)
        float rollDrag = Mathf.Abs(rollAngle) * 0.3f;

        // Target speed based on pitch and roll
        float targetSpeed = Mathf.Lerp(minForwardSpeed, maxForwardSpeed,
            (1f - Mathf.Abs(pitchAngle)) * (1f - rollDrag));

        // Smooth speed changes
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 2f);

        Vector3 forwardThrust = transform.forward * currentSpeed;

        // Base lift from forward speed
        float speedLift = 4f * (currentSpeed / maxForwardSpeed);

        // Lift - nose up generates more lift, but too much causes stall
        float angleOfAttack = pitchAngle * 1.5f;
        float aoaLift = angleOfAttack * 3f;

        // Stall effect - if pitch is too high and speed is low, lose lift
        float stallThreshold = 0.6f;
        float stallMultiplier = 1f;
        if (pitchAngle > stallThreshold && currentSpeed < minForwardSpeed * 1.2f)
        {
            // Stall! Rapid loss of lift
            stallMultiplier = Mathf.Lerp(1f, 0.2f, (pitchAngle - stallThreshold) / 0.4f);
        }

        // Combined lift force
        float totalLift = (speedLift + aoaLift) * stallMultiplier;

        // Net vertical force (positive = up, negative = down)
        float verticalForce = totalLift + gravity;

        // Add extra lift during flapping
        if (isFlapping)
        {
            verticalForce += 8f;
            // Flapping gives a temporary speed boost
            currentSpeed += Time.deltaTime * 5f;
            currentSpeed = Mathf.Min(currentSpeed, maxForwardSpeed);
        }

        // Apply forces to Rigidbody
        Vector3 totalForce = forwardThrust + (Vector3.up * verticalForce);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, totalForce, Time.deltaTime);

        // Add drag based on speed and angle of attack
        float aoaDrag = Mathf.Abs(pitchAngle) * 1.5f;
        rb.linearDamping = glideDrag + aoaDrag;

        // Terminal velocity limits
        float maxFallSpeed = -12f;
        float maxClimbSpeed = 15f;

        if (rb.linearVelocity.y < maxFallSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxFallSpeed, rb.linearVelocity.z);
        else if (rb.linearVelocity.y > maxClimbSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxClimbSpeed, rb.linearVelocity.z);

    }

    void FlapWings()
    {
        if (isFlapping) return;
        StartCoroutine(FlapCoroutine());

        // Flap force depends on current speed and pitch
        float pitchFactor = 1f - Mathf.Abs(transform.forward.y) * 0.5f;
        float speedFactor = Mathf.Clamp01(currentSpeed / maxForwardSpeed);
        float flapStrength = flapForce * (0.8f + speedFactor * 0.4f) * pitchFactor;

        rb.AddForce(Vector3.up * flapStrength, ForceMode.Impulse);

        // Add slight forward boost during flap
        rb.AddForce(transform.forward * flapStrength * 0.3f, ForceMode.Impulse);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 2f);
        rb.linearVelocity = velocity;

        // Small speed boost during flap
        currentSpeed += 1f;
        currentSpeed = Mathf.Min(currentSpeed, maxForwardSpeed);
    }

    IEnumerator FlapCoroutine()
    {
        if (isFlapping) yield break;
        isFlapping = true;
        float elapsed = 0;
        float flapDuration = 0.2f;

        // Flap down
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

        // Flap up
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
        if (!isFlapping && leftWing != null && rightWing != null)
        {
            float glideAngle = Mathf.Sin(Time.time * 2f) * 5f;
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
    }

    public void CalibrateNeutralPosition()
    {
        if (leftController != null && rightController != null)
        {
            calibratedLeftRotation = leftController.localEulerAngles;
            calibratedRightRotation = rightController.localEulerAngles;

            // Store the current rotation as the neutral position
            calibratedNeutralRotation = transform.rotation;

            isCalibrated = true;

            // Reset any flapping states
            leftWingFlapped = false;
            rightWingFlapped = false;

            // Clear angular velocity
            if (rb != null)
            {
                rb.angularVelocity = Vector3.zero;
            }

            // Reset target rotation to neutral
            targetRotation = calibratedNeutralRotation;
        }
    }

    void ApplyAreaConstraints()
    {
        Vector3 pos = transform.position;

        if (pos.z > areaLength)
        {
            pos.z = areaLength;
        }
        else if (pos.z < 0)
        {
            pos.z = 0;
        }

        if (Mathf.Abs(pos.x) > areaWidth / 2)
        {
            pos.x = Mathf.Sign(pos.x) * areaWidth / 2;
        }

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
        // Check if bird has reached the win Z position and hasn't won yet
        if (!hasWon && !winTriggered && transform.position.z >= winZPosition)
        {
            TriggerWin();
        }
    }

    void TriggerWin()
    {
        winTriggered = true;
        hasWon = true;

        // Stop all movement
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * 20;
        }

        // Stop any ongoing coroutines
        StopAllCoroutines();

        // Disable flaps
        flapsEnabled = false;
        isFlapping = false;

        // Load the win scene after delay
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

        // Draw forward direction arrow
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        // Draw right direction
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.right * 3f);
    }

    void OnGUI()
    {
        GUILayout.Label("=== BIRD FLIGHT CONTROLLER ===");
        GUILayout.Label($"Position: {transform.position:F1}");
        GUILayout.Label($"Speed: {currentSpeed:F1}");
        GUILayout.Label($"Forward: {transform.forward:F2}");
        GUILayout.Label($"Velocity: {rb.linearVelocity:F1}");

        if (leftController != null && rightController != null)
        {
            GUILayout.Label($"Rotation: {transform.eulerAngles:F0}");
            GUILayout.Label($"Is Flapping: {isFlapping}");

            if (!flapsEnabled && !keyControls)
            {
                float timeRemaining = startupFlapDelay - startupTimer;
                GUILayout.Label($"⚠ Flaps ready in: {timeRemaining:F1}s");
            }

            if (isCalibrated)
            {
                GUILayout.Label("✓ Calibrated - Bird follows controllers");
            }
            else
            {
                GUILayout.Label("⚠ Not Calibrated - Press A button");
            }
        }

        if (collisionHandler != null)
        {
            GUILayout.Label($"Health: {collisionHandler.GetCurrentHealth()}/{collisionHandler.GetMaxHealth()}");
        }
    }

    //For resetting flap states after collision
    public void ResetFlapStates()
    {
        // Reset all flap-related variables
        isFlapping = false;
        leftWingFlapped = false;
        rightWingFlapped = false;
        leftFlapTime = 0f;
        rightFlapTime = 0f;

        // Stop any ongoing flap coroutine
        StopAllCoroutines();

        // Reset wing positions to neutral
        if (leftWing != null)
            leftWing.localRotation = Quaternion.identity;
        if (rightWing != null)
            rightWing.localRotation = Quaternion.identity;

        // Clear any pending input that might cause immediate flapping
        if (leftController != null)
            previousLeftControllerPos = leftController.position;
        if (rightController != null)
            previousRightControllerPos = rightController.position;
    }
}