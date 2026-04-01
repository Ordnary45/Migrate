using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BirdCollisionHandler : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private float pushbackDistance = 40f;
    [SerializeField] private float collisionCooldown = 2f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Building Collision")]
    [SerializeField] private BuildingMapGenerator buildingMapGenerator;

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    [Header("Effects")]
    [SerializeField] private AudioClip collisionSound;
    [SerializeField] private float invincibilityDuration = 1.5f;

    [Header("UI References")]
    [SerializeField] private Text healthText;

    [Header("Death Scene")]
    [SerializeField] private string loseSceneName = "You lose";
    [SerializeField] private float deathSceneDelay = 2f;

    // Components
    private BirdFlightController flightController;
    private Rigidbody rb;
    private Collider birdCollider;
    private AudioSource audioSource;
    private float originalAngularDrag;

    // State tracking
    private bool isInvincible = false;
    private bool isRecovering = false;
    private float collisionTimer = 0f;
    private bool isDead = false;

    // Events
    public System.Action<int> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action OnCollision;
    public bool IsRecovering => isRecovering;

    void Start()
    {
        flightController = GetComponent<BirdFlightController>();
        rb = GetComponent<Rigidbody>();
        birdCollider = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (rb != null)
        {
            originalAngularDrag = rb.angularDamping;
        }

        currentHealth = maxHealth;
        UpdateHealthDisplay();

        isDead = false;
    }

    void Update()
    {
        if (collisionTimer > 0)
            collisionTimer -= Time.deltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!ShouldProcessCollision())
            return;

        HandleCollision();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!ShouldProcessCollision())
            return;

        HandleCollision();
    }

    bool ShouldProcessCollision()
    {
        if (isInvincible || collisionTimer > 0 || isRecovering)
            return false;

        return true;
    }

    void HandleCollision()
    {
        // Start cooldown
        collisionTimer = collisionCooldown;

        // Stop all movement and rotation
        StopBirdMovement();

        // Reset flap states before disabling controller
        ResetFlapStates();

        // Reduce health
        TakeDamage(1);

        // Move bird backward
        StartCoroutine(MoveBirdBackward());

        // Play sound
        PlayCollisionSound();
    }

    void StopBirdMovement()
    {
        if (rb != null)
        {
            // Stop all movement
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset rotation to stable orientation
        Vector3 forward = transform.forward;
        forward.y = Mathf.Clamp(forward.y, -0.5f, 0.5f);
        forward.Normalize();
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void ResetFlapStates()
    {
        if (flightController != null)
        {
            flightController.ResetFlapStates();
        }
    }

    IEnumerator MoveBirdBackward()
    {
        isRecovering = true;

        // Disable flight control during movement
        if (flightController != null) {
            flightController.enabled = false;
            flightController.ResetFlapStates();
        }

        float areaLength = flightController.GetAreaLength();
        float minHeight = flightController.GetMinHeight();
        float maxHeight = flightController.GetMaxHeight();

        // Calculate new position
        Vector3 newPosition = transform.position;
        newPosition.z -= pushbackDistance;

        // Keep within world limits
        newPosition.x = Mathf.Clamp(newPosition.x, 0, areaLength);
        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
        newPosition.z = Mathf.Clamp(newPosition.z, 0, areaLength);

        // Check if the new position is colliding
        if (IsPositionColliding(newPosition))
        {
            // Try to find a safe position by moving back in smaller increments
            Debug.Log("Target position is colliding, searching for safe position...");

            for (float distance = pushbackDistance; distance > 0; distance -= 2f)
            {
                Vector3 testPosition = transform.position;
                testPosition.x -= distance;
                testPosition.x = Mathf.Max(0, testPosition.x);

                if (!IsPositionColliding(testPosition))
                {
                    newPosition = testPosition;
                    Debug.Log($"Found safe position.");
                    break;
                }
            }
        }

        // Move the bird to the new position
        transform.position = newPosition;

        // Small delay for stability
        yield return new WaitForSeconds(0.2f);

        // Restore normal angular drag
        if (rb != null)
            rb.angularDamping = originalAngularDrag;

        // Reset flap states again before re-enabling
        if (flightController != null)
            flightController.ResetFlapStates();

        // Re-enable flight control
        if (flightController != null)
            flightController.enabled = true;

        isRecovering = false;
    }

    bool IsPositionColliding(Vector3 position)
    {
        if (birdCollider == null) return false;

        // Check if the position would overlap with any obstacles
        Collider[] colliders = Physics.OverlapSphere(position, birdCollider.bounds.extents.magnitude, obstacleLayers);

        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject && !col.isTrigger)
            {
                // Check if it's a valid obstacle (not the player or ignored objects)
                if (!col.CompareTag("Player") && !col.CompareTag("IgnoreCollision"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void TakeDamage(int damage)
    {
        if (isInvincible || isRecovering) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        UpdateHealthDisplay();
        OnHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvincibilityRoutine());
        }
    }

    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float flashInterval = 0.1f;
        float endTime = Time.time + invincibilityDuration;

        while (Time.time < endTime)
        {
            foreach (Renderer r in renderers)
            {
                r.enabled = !r.enabled;
            }
            yield return new WaitForSeconds(flashInterval);
        }

        foreach (Renderer r in renderers)
        {
            r.enabled = true;
        }

        isInvincible = false;
    }

    void PlayCollisionSound()
    {
        if (collisionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collisionSound);
        }
    }

    void UpdateHealthDisplay()
    {
        if (healthText != null)
        {
            healthText.text = $"Health: {currentHealth}/{maxHealth}";
        }
    }

    void Die()
    {
        isDead = true;

        Debug.Log("Bird has died!");

        if (flightController != null)
            flightController.enabled = false;

        if (birdCollider != null)
            birdCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
        }

        OnDeath?.Invoke();
        StartCoroutine(DeathRoutine());

        buildingMapGenerator = GetComponent<BuildingMapGenerator>();
        buildingMapGenerator.ClearMap();
    }

    IEnumerator DeathRoutine()
    {
        // Wait for death scene delay
        yield return new WaitForSeconds(deathSceneDelay);
        SceneManager.LoadScene(loseSceneName);

    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;

    void OnDrawGizmosSelected()
    {
        // Visualize pushback distance in editor
        Gizmos.color = Color.red;
        Vector3 pushbackEnd = transform.position + (Vector3.back * pushbackDistance);
        Gizmos.DrawLine(transform.position, pushbackEnd);
        Gizmos.DrawWireSphere(pushbackEnd, 1f);
    }
}