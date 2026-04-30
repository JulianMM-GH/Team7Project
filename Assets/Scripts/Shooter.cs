using SupanthaPaul;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.U2D.ScriptablePacker;

public class Shooter : MonoBehaviour
{
    [SerializeField] public static PlayerInput PlayerInput;

    private InputAction shootAction;

    [SerializeField] public bool canShoot = false;

    public static bool ShootWasPressed;
    public static bool ShootWasReleased;

    [SerializeField] public ProjectileBehaviour projectilePrefab;
    [SerializeField] Transform LaunchOffset;

    void Awake()
    {
        PlayerInput = GetComponent<PlayerInput>();

        shootAction = PlayerInput.actions["Shoot"];
    }

    private void Update()
    {
        ShootWasPressed = shootAction.WasPressedThisFrame();
        ShootWasReleased = shootAction.WasReleasedThisFrame();

        /*
        if (ShootWasPressed && canShoot)
        {
            Instantiate(projectilePrefab, LaunchOffset.position, transform.rotation);
        }
        */

        if (ShootWasReleased && canShoot)
        {
            Instantiate(projectilePrefab, LaunchOffset.position, transform.rotation);
        }
    }
}
