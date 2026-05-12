using SupanthaPaul;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhotoItem : MonoBehaviour
{
    [Header("Target Scripts")]
    public PlayerController playerController1;
    public PlayerController playerController2;

    private Shooter shooter;
    private LightEffect lightEffect;

    [Header("Abilities To Enable")]
    public bool canWallJump;
    public bool canLightEffect;
    public bool canDoubleJump;
    public bool canShoot;

    [Header("Movement")]
    public float BobHeight = 0.5f;
    public float BobSpeed = 2f;
    public float SpinSpeed = 90f;

    [Header("Trigger")]
    public string GameObjectName = "Player1";

    [Header("UI")]
    public GameObject photoFrame;

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

    void Start()
    {
        startPosition = transform.position;

        if (lightEffect == null)
            lightEffect = FindAnyObjectByType<LightEffect>();

        if (shooter == null && playerController1 != null)
            shooter = playerController1.gameObject.GetComponent<Shooter>();

        if (photoFrame == null)
        {
            Debug.LogError("PhotoItem: photoFrame is not assigned.");
            return;
        }

        photoFrame.SetActive(true);

        canvasGroup = photoFrame.GetComponent<CanvasGroup>();

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name != GameObjectName)
            return;

        EnableAbilities();

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

    private void EnableAbilities()
    {
        if (canLightEffect && lightEffect != null)
            lightEffect.canLight = true;

        if (canShoot && shooter != null)
            shooter.canShoot = true;

        if (canWallJump)
        {
            if (playerController1 != null)
                playerController1.canWallJump = true;

            if (playerController2 != null)
                playerController2.canWallJump = true;
        }

        if (canDoubleJump)
        {
            if (playerController1 != null)
                playerController1.canDoubleJump = true;

            if (playerController2 != null)
                playerController2.canDoubleJump = true;
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