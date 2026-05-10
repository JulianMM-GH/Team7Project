using SupanthaPaul;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Shooter : MonoBehaviour
{
    [SerializeField] public static PlayerInput PlayerInput;

    private InputAction shootAction;

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

    void Start()
    {
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
    }

    private void Update()
    {
        // Check if the slingshot is on cooldown
        bool isOffCooldown = Time.time >= cooldownTimer;

        if (shootAction.IsPressed() && canShoot && isOffCooldown)
        {
            chargeBar.gameObject.SetActive(true);

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

            FireProjectile();

            // Set cooldown timestamp
            cooldownTimer = Time.time + cooldown;

            ResetProjectileCharge();
        }
    }

    void FireProjectile()
    {
        GameObject projectile = Instantiate(projectilePrefab, LaunchOffset.position, transform.rotation);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        // Determine direction based on the player's local scale (flipping direction)
        float direction = transform.localScale.x > 0 ? -1f : 1f;

        // Determine angled vector
        Vector2 launchDirection = new Vector2(direction, upwardForce).normalized;

        // Apply force to the projectile
        float totalSpeed = minSpeed + (chargeTime * speedMultiplier);
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

        // Determine direction based on the player's local scale (flipping direction)
        float direction = transform.localScale.x > 0 ? -1f : 1f;

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

            if (hit.collider != null)
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
}
