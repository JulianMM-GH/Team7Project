using SupanthaPaul;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Added to check PlayerInput tracking indices

public class PhotoItem : MonoBehaviour
{
    // Simple enum structure to pick target rules cleanly in the inspector dropdown
    public enum TargetPlayerRule { BothPlayers, Player1Only, Player2Only }

    [Header("Target Configuration")]
    [SerializeField] private TargetPlayerRule targetRestriction = TargetPlayerRule.BothPlayers;

    [Header("Abilities To Enable")]
    public bool canWallJump;
    public bool canLightEffect;
    public bool canDoubleJump;
    public bool canShoot;

    [Header("Movement")]
    public float BobHeight = 0.5f;
    public float BobSpeed = 2f;
    public float SpinSpeed = 90f;

    [Header("UI")]
    public GameObject photoFrame;
    public GameObject ButtonPrompt;

    public string photoImageChildName = "photoImage";
    public Sprite PheonixImage;

    public string photoTextChildName = "photoText";
    public string PheonixText = "Pheonix";

    [Header("Fade")]
    public float fadeTime = 1f;
    public float stayTime = 1f;

    private Vector3 startPosition;
    private CanvasGroup canvasGroup;
    private Image photoImage;
    private TMP_Text photoText;
    private Coroutine fadeCoroutine;
    private bool hasBeenPickedUp = false;

    void Start()
    {
        startPosition = transform.position;

        if (photoFrame == null)
        {
            Debug.LogError("PhotoItem: photoFrame is not assigned.");
            return;
        }

        photoFrame.SetActive(true);
        canvasGroup = photoFrame.GetComponent<CanvasGroup>();
        ButtonPrompt.SetActive(false);

        if (canvasGroup == null)
            canvasGroup = photoFrame.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;

        photoImage = FindChildImage(photoImageChildName);
        photoText = FindChildText(photoTextChildName);

        if (photoImage == null)
            Debug.LogError("PhotoItem: Could not find Image child named " + photoImageChildName);

        if (photoText == null)
            Debug.LogError("PhotoItem: Could not find TextMeshPro child named " + photoTextChildName);
    }

    void Update()
    {
        transform.Rotate(0f, SpinSpeed * Time.deltaTime, 0f);

        float bob = Mathf.Sin(Time.time * BobSpeed) * BobHeight;
        transform.position = startPosition + new Vector3(0f, bob, 0f);

    }

    private void OnTriggerExit2D(UnityEngine.Collider2D collision)
    {
        ButtonPrompt.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp) return;
        if (!Input.GetKey(KeyCode.F)) {
            ButtonPrompt.SetActive(true);
            return;    
        }
        // Verify the triggering object belongs to an actual player setup
        PlayerController touchingPlayer = other.GetComponentInParent<PlayerController>();
        if (touchingPlayer == null) return;

        hasBeenPickedUp = true;

        // Execute dynamic targeted unlocking
        EnableAbilitiesFiltered();

        if (photoImage != null)
            photoImage.sprite = PheonixImage;

        if (photoText != null)
            photoText.text = PheonixText;

        if (canvasGroup == null)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeInThenOut());
    }

    private void EnableAbilitiesFiltered()
    {
        PlayerController[] allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController player in allPlayers)
        {
            if (player == null) continue;

            // Fetch the Input System wrapper component from the player root
            PlayerInput inputComp = player.GetComponent<PlayerInput>();
            if (inputComp == null) continue;

            // Enforce rules using the official player placement index (0 = P1, 1 = P2)
            if (targetRestriction == TargetPlayerRule.Player1Only && inputComp.playerIndex != 0) continue;
            if (targetRestriction == TargetPlayerRule.Player2Only && inputComp.playerIndex != 1) continue;

            // Apply selected upgrades to authorized characters
            if (canWallJump)
                player.canWallJump = true;

            if (canDoubleJump)
                player.canDoubleJump = true;

            if (canLightEffect)
            {
                LightEffect localLight = player.GetComponentInChildren<LightEffect>(true);
                if (localLight != null)
                {
                    localLight.canLight = true;
                }
            }

            if (canShoot)
            {
                Shooter localShooter = player.GetComponent<Shooter>();
                if (localShooter != null)
                {
                    localShooter.canShoot = true;
                }
            }
        }
    }

    private IEnumerator FadeInThenOut()
    {
        yield return FadeTo(1f);

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer.enabled = false;
        }

        yield return new WaitForSeconds(stayTime);

        yield return FadeTo(0f);

        Destroy(gameObject);
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeTime);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private Image FindChildImage(string childName)
    {
        Image[] images = photoFrame.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.gameObject.name == childName)
                return image;
        }
        return null;
    }

    private TMP_Text FindChildText(string childName)
    {
        TMP_Text[] texts = photoFrame.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name == childName)
                return text;
        }
        return null;
    }
}