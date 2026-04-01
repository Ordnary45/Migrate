using UnityEngine;
using System.Collections;

public class FlockFlight : MonoBehaviour
{
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;
    [SerializeField] private float wingFlapAngle = 30f;
    [SerializeField] private float flapDuration = 0.2f;

    [Header("Flight Movement")]
    [SerializeField] private bool moveInCircle = true;
    [SerializeField] private float circleRadius = 5f;
    [SerializeField] private float circleSpeed = 1f;
    [SerializeField] private float floatAmplitude = 0.5f;
    [SerializeField] private float floatSpeed = 1f;

    [Header("Randomization")]
    [SerializeField] private float minStartDelay = 0f;
    [SerializeField] private float maxStartDelay = 1f;
    [SerializeField] private float minFlapSpeed = 0.8f;
    [SerializeField] private float maxFlapSpeed = 1.2f;

    private Vector3 startPosition;
    private float flapSpeed;
    private bool isFlapping = true;
    private float circleAngle = 0f;
    private float floatOffset = 0f;
    private Coroutine flapCoroutine;

    private Quaternion leftWingOriginalRotation;
    private Quaternion rightWingOriginalRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        {
            // Store original wing rotations
            if (leftWing != null)
                leftWingOriginalRotation = leftWing.localRotation;
            if (rightWing != null)
                rightWingOriginalRotation = rightWing.localRotation;

            // Store start position
            startPosition = transform.position;

            // Randomize float offset for variety
            floatOffset = Random.Range(0f, Mathf.PI * 2);

            // Randomize flap speed
            flapSpeed = Random.Range(minFlapSpeed, maxFlapSpeed);

            // Start wing animation with delay
            float delay = Random.Range(minStartDelay, maxStartDelay);
            StartCoroutine(StartFlappingWithDelay(delay));
        }
    }

    void Update()
    {
        {
            // Update circle angle
            circleAngle += Time.deltaTime * circleSpeed;

            // Calculate circle position
            float x = startPosition.x + Mathf.Cos(circleAngle) * circleRadius;
            float z = startPosition.z + Mathf.Sin(circleAngle) * circleRadius;

            // Add floating motion
            float y = startPosition.y + Mathf.Sin(Time.time * floatSpeed + floatOffset) * floatAmplitude;

            // Update position
            transform.position = new Vector3(x, y, z);

            // Make bird face direction of movement
            Vector3 direction = new Vector3(-Mathf.Sin(circleAngle), 0, Mathf.Cos(circleAngle));
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }

    IEnumerator StartFlappingWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        flapCoroutine = StartCoroutine(FlapCoroutine());
    }


    IEnumerator FlapCoroutine()
    {
        while (isFlapping) // Infinite loop
        {
            float elapsed = 0;
            float currentFlapDuration = flapDuration / flapSpeed;

            // Flap down
            while (elapsed < currentFlapDuration)
            {
                float t = elapsed / currentFlapDuration;
                float angle = Mathf.Sin(t * Mathf.PI) * wingFlapAngle;

                if (leftWing != null)
                    leftWing.localRotation = leftWingOriginalRotation * Quaternion.Euler(angle, 0, 0);
                if (rightWing != null)
                    rightWing.localRotation = rightWingOriginalRotation * Quaternion.Euler(-angle, 0, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Flap up
            elapsed = 0;
            while (elapsed < currentFlapDuration)
            {
                float t = elapsed / currentFlapDuration;
                float angle = Mathf.Sin(t * Mathf.PI) * wingFlapAngle;

                if (leftWing != null)
                    leftWing.localRotation = leftWingOriginalRotation * Quaternion.Euler(angle, 0, 0);
                if (rightWing != null)
                    rightWing.localRotation = rightWingOriginalRotation * Quaternion.Euler(-angle, 0, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
