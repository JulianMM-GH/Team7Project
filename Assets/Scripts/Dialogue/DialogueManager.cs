using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI")]
    [SerializeField] private RectTransform dialogueBox;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Positions")]
    [SerializeField] private Vector2 hiddenPosition;
    [SerializeField] private Vector2 shownPosition;

    [Header("Settings")]
    [SerializeField] private float slideSpeed = 8f;
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Text Sizing")]
    [Tooltip("Font is sized once to fit the whole line, then stays fixed while it's revealed")]
    [SerializeField] private float minFontSize = 34f;
    [SerializeField] private float maxFontSize = 60f;

    [Header("Auto-Hide")]
    [Tooltip("How long the dialogue stays on screen (after typing finishes) before it hides itself")]
    [SerializeField] private float stayDuration = 3f;
    [SerializeField] private bool slideAwayOnHide = true;
    [SerializeField] private bool fadeAwayOnHide = false;
    [SerializeField] private float fadeDuration = 0.5f;

    private CanvasGroup dialogueCanvasGroup;
    private Coroutine dialogueCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        dialogueBox.anchoredPosition = hiddenPosition;
        dialogueText.text = "";

        // wrap + auto-size within the box so long lines shrink to fit instead of spilling out
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = minFontSize;
        dialogueText.fontSizeMax = maxFontSize;

        dialogueCanvasGroup =dialogueBox.GetComponent<CanvasGroup>();
        if (dialogueCanvasGroup == null)
            dialogueCanvasGroup = dialogueBox.gameObject.AddComponent<CanvasGroup>();
    }

    public void ShowDialogue(string text)
    {
        if (dialogueCoroutine != null)
            StopCoroutine(dialogueCoroutine);

        dialogueCoroutine = StartCoroutine(DialogueSequence(text));
    }

    private IEnumerator DialogueSequence(string text)
    {
        // lay out the full line up front (so font size + wrapping are final), then reveal it left to right
        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        dialogueCanvasGroup.alpha = 1f;

        while (Vector2.Distance(dialogueBox.anchoredPosition, shownPosition) > 0.1f)
        {
            dialogueBox.anchoredPosition = Vector2.Lerp(
                dialogueBox.anchoredPosition,
                shownPosition,
                slideSpeed * Time.deltaTime
            );

            yield return null;
        }

        dialogueBox.anchoredPosition = shownPosition;
        int totalCharacters = dialogueText.textInfo.characterCount;
        for (int i = 1; i <= totalCharacters; i++)
        {
            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typingSpeed);
        }

        yield return new WaitForSeconds(stayDuration);

        yield return HideDialogue();
    }

    private IEnumerator HideDialogue()
    {
        bool positionDone = !slideAwayOnHide;
        bool fadeDone = !fadeAwayOnHide;
        float fadeTimer = 0f;
        float startAlpha = dialogueCanvasGroup.alpha;

        while (!positionDone || !fadeDone)
        {
            if (!positionDone)
            {
                dialogueBox.anchoredPosition = Vector2.Lerp(
                    dialogueBox.anchoredPosition,
                    hiddenPosition,
                    slideSpeed * Time.deltaTime
                );

                if (Vector2.Distance(dialogueBox.anchoredPosition, hiddenPosition) <= 0.1f)
                {
                    dialogueBox.anchoredPosition = hiddenPosition;
                    positionDone = true;
                }
            }

            if (!fadeDone)
            {
                fadeTimer += Time.deltaTime;
                float t = Mathf.Clamp01(fadeTimer / fadeDuration);
                dialogueCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                fadeDone = t >= 1f;
            }

            yield return null;
        }

        // neither toggle enabled - snap hidden instantly instead of leaving it on screen
        if (!slideAwayOnHide)
            dialogueBox.anchoredPosition = hiddenPosition;

        dialogueText.text = "";
        dialogueCanvasGroup.alpha = 1f; // reset so it's opaque again next time it's shown
    }
}
