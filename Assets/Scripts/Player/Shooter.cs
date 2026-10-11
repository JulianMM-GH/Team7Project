using SupanthaPaul;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class Shooter : MonoBehaviour
{
    [SerializeField] public static PlayerInput PlayerInput;

    private InputAction shootAction;

    private InputAction moveAction;
    private InputAction jumpAction;

    [Header("Input Action Names")]
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";

    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] Transform LaunchOffset;

    [Header("Slingshot / Projectile")]
    [SerializeField] public bool canShoot = false;

    [SerializeField] public float chargeTime = 0;
    [SerializeField] public float maxChargeTime = 3f;

    [SerializeField] public float minSpeed = 1;
    [SerializeField] public float speedMultiplier = 1;

    [SerializeField] private float upwardForce = 0.5f;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 2.0f;
    private float cooldownTimer = 0f;

    [Header("Projectile Charge UI")]
    [SerializeField] public Slider chargeBar;
    [SerializeField] public Image barFill;
    [SerializeField] public Gradient chargeGradient;

    [Header("Trajectory")] public Gradient chargeGradient2;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private int resolution = 30;
    [SerializeField] private float stepTime = 0.1f;

    [Header("Animation Tuning")]
    [SerializeField] private float projectileSpawnDelay = 0.35f;
    [SerializeField] private float totalShootLockoutDuration = 0.5f;

    private PlayerController m_playerController;
    private Animator m_animator;
    private Rigidbody2D m_rb2d;

    private bool isCharging = false;
    private bool isLockoutActive = false;

    void Start()
    {
        m_playerController = GetComponent<PlayerController>();
        if (m_playerController == null)
        {
            m_playerController = GetComponentInParent<PlayerController>();
        }

        if (m_playerController != null)
        {
            m_animator = m_playerController.GetComponentInChildren<Animator>();
        }
        else
        {
            m_animator = GetComponentInChildren<Animator>();
        }

        if (chargeBar != null)
        {
            chargeBar.minValue = 0;
            chargeBar.maxValue = maxChargeTime;
            chargeBar.value = 0;
        }
    }

    void Awake()
    {
        chargeBar.gameObject.SetActive(false);
        lineRenderer.enabled = false;

        PlayerInput = GetComponent<PlayerInput>();

        shootAction = PlayerInput.actions["Shoot"];

        moveAction = PlayerInput.actions[moveActionName];
        jumpAction = PlayerInput.actions[jumpActionName];
    }

    private void Update()
    {
        // Check if the slingshot is on cooldown
        bool isOffCooldown = Time.time >= cooldownTimer;

        bool isPlayerGrounded = m_playerController != null && m_playerController.isGrounded;

        if (shootAction.IsPressed() && canShoot && isOffCooldown && isPlayerGrounded && !isLockoutActive)
        {
            if (!isCharging)
            {
                StartCharging();
            }

            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0f, maxChargeTime);

            // Show and update trajectory
            lineRenderer.enabled = true;
            DrawTrajectory();

            UpdateSlingshotUI();
        }

        if (shootAction.WasReleasedThisFrame() && canShoot && chargeTime > 0)
        {
            //Hide charge bar
            chargeBar.gameObject.SetActive(false);

            // Hide trajectory
            lineRenderer.enabled = false;

            StartCoroutine(ShootSequenceCoroutine(chargeTime));

            // Set cooldown timestamp
            cooldownTimer = Time.time + cooldown;

            ResetProjectileCharge();
        }

        // Catch instances where the player releases early without executing a shot
        else if (shootAction.WasReleasedThisFrame() && isCharging)
        {
            if (chargeBar != null) chargeBar.gameObject.SetActive(false);
            if (lineRenderer != null) lineRenderer.enabled = false;

            CancelCharge();

            ResetProjectileCharge();
        }
    }

    private void StartCharging()
    {
        isCharging = true;

        if (chargeBar != null) chargeBar.gameObject.SetActive(true);

        if (m_rb2d != null)
        {
            m_rb2d.linearVelocity = new Vector2(0f, m_rb2d.linearVelocity.y);
        }

        if (moveAction != null) moveAction.Disable();
        if (jumpAction != null) jumpAction.Disable();

        if (m_animator != null)
        {
            m_animator.SetBool("isPullingSlingshot", true);
        }
    }

    // Slightly delayed shot to match the newly added animation
    // Player is locked, can't use movement or jump
    // Can't move still for a moment after shooting, ensuring effective animation transition
    private IEnumerator ShootSequenceCoroutine(float finalizedChargeTime)
    {
        isCharging = false;
        isLockoutActive = true;

        if (m_animator != null)
        {
            m_animator.SetBool("isPullingSlingshot", false);
            m_animator.SetTrigger("shootSlingshot");
        }

        yield return new WaitForSeconds(projectileSpawnDelay);

        FireProjectile(finalizedChargeTime);

        float remainingLockoutTime = Mathf.Max(0f, totalShootLockoutDuration - projectileSpawnDelay);
        yield return new WaitForSeconds(remainingLockoutTime);

        isLockoutActive = false;
        if (moveAction != null) moveAction.Enable();
        if (jumpAction != null) jumpAction.Enable();
    }

    private void CancelCharge()
    {
        isCharging = false;

        if (moveAction != null) moveAction.Enable();
        if (jumpAction != null) jumpAction.Enable();

        if (m_animator != null)
        {
            m_animator.SetBool("isPullingSlingshot", false);
        }
    }

    void FireProjectile(float finalCharge)
    {
        RAudio.PlayOneShot("Slingshot");

        GameObject projectile = Instantiate(projectilePrefab, LaunchOffset.position, transform.rotation);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        // Check m_facingRight from player controller
        // Otherwise, default to the original logic.
        float direction = 1f;
        if (m_playerController != null)
        {
            direction = m_playerController.m_facingRight ? 1f : -1f;
        }
        else
        {
            direction = transform.localScale.x > 0 ? 1f : -1f;
        }

        // Determine angled vector
        Vector2 launchDirection = new Vector2(direction, upwardForce).normalized;

        // Apply force to the projectile
        //float totalSpeed = minSpeed + (chargeTime * speedMultiplier);
        float totalSpeed = minSpeed + (finalCharge * speedMultiplier);
        Vector2 force = launchDirection * totalSpeed;

        rb.AddForce(force, ForceMode2D.Impulse);
    }

    void UpdateSlingshotUI()
    {
        if (chargeBar == null) return;

        // Calculates from 0 to 1 for the gradient charging effect
        float normalizedCharge = chargeTime / maxChargeTime;
        chargeBar.value = chargeTime;

        if (barFill != null)
        {
            barFill.color = chargeGradient.Evaluate(normalizedCharge);
        }
    }

    void DrawTrajectory()
    {
        // Draw the trajectory from the Launch Offset postion, as that is the projectiles starting point
        Vector2 startPos = LaunchOffset.position;

        // Determine direction
        float direction = 1f;
        if (m_playerController != null)
        {
            direction = m_playerController.m_facingRight ? 1f : -1f;
        }
        else
        {
            direction = transform.localScale.x > 0 ? 1f : -1f;
        }

        // Calculate the same force used in FireProjectile (found under "Apply force to projectile")
        float currentSpeed = minSpeed + (chargeTime * speedMultiplier);

        // Use direction to create the velocity vector
        //Vector2 velocity = new Vector2(direction, 0) * currentSpeed;

        // Match the direction from FireProjectile
        Vector2 launchDirection = new Vector2(direction, upwardForce).normalized;

        // Calculate velocity
        Vector2 velocity = launchDirection * currentSpeed;

        // Account for Rigidbody2D Mass
        float mass = projectilePrefab.GetComponent<Rigidbody2D>().mass;
        velocity /= mass;

        lineRenderer.positionCount = resolution;

        Vector2 previousPos = startPos;

        for (int i = 0; i < resolution; i++)
        {
            float t = i * stepTime;
            // Gravity Formula: s = ut + 0.5at^2 (where a is gravity)
            Vector2 pos = startPos + velocity * t + 0.5f * Physics2D.gravity * t * t;

            // Check for collision between the last point and the current point
            RaycastHit2D hit = Physics2D.Linecast(previousPos, pos);

            if (hit.collider != null && !hit.collider.CompareTag("Light") && !hit.collider.CompareTag("Tutorial"))
            {
                // Set the current point to the collision spot
                lineRenderer.SetPosition(i, hit.point);

                // Adjust line length to end here and stop calculating
                lineRenderer.positionCount = i + 1;
                break;
            }

            lineRenderer.SetPosition(i, pos);
            previousPos = pos;
        }
    }

    void ResetProjectileCharge()
    {
        //Resets projectile charge, including UI elements
        chargeTime = 0f;
        if (chargeBar != null) chargeBar.value = 0;
        if (barFill != null) barFill.color = chargeGradient.Evaluate(0);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (isCharging)
        {
            CancelCharge();
        }
    }
}