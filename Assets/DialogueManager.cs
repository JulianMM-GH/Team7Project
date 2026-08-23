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
    }

    public void ShowDialogue(string text)
    {
        if (dialogueCoroutine != null)
            StopCoroutine(dialogueCoroutine);

        dialogueCoroutine = StartCoroutine(DialogueSequence(text));
    }

    private IEnumerator DialogueSequence(string text)
    {
        dialogueText.text = "";

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
        foreach (char letter in text)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}