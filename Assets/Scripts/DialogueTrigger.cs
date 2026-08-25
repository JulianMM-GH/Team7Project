using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DialogueTrigger : MonoBehaviour
{
    [TextArea(3, 8)]
    public string dialogueText;

    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (triggerOnce && hasTriggered)
            return;

        hasTriggered = true;

        DialogueManager.Instance.ShowDialogue(dialogueText);
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = oldMatrix;
        }

#if UNITY_EDITOR
        Vector3 labelPosition = transform.position;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.red;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 10;

        Handles.Label(labelPosition, "DIALOGUE TRIGGER", style);
#endif
    }
}