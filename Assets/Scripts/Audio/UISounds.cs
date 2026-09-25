using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover + click sounds for UI, works with mouse and controller navigation
/// </summary>
[AddComponentMenu("Audio/UI Sounds")]
public class UISounds : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
	[SerializeField] private EventReference hover;
	[SerializeField] private EventReference click;
	[Tooltip("Plays instead of click when the button isn't interactable")]
	[SerializeField] private EventReference clickWhileDisabled;
	[Tooltip("Menus select their first button when they open, don't play hover for that")]
	[SerializeField] private bool skipHoverWhenMenuOpens = true;

	private Selectable m_selectable;
	private int m_enabledFrame;

	private void Awake() => m_selectable = GetComponent<Selectable>();

	private void OnEnable() => m_enabledFrame = Time.frameCount;

	private bool Interactable => m_selectable == null || m_selectable.IsInteractable();

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (Interactable) Play(hover);
	}

	public void OnSelect(BaseEventData eventData)
	{
		if (skipHoverWhenMenuOpens && Time.frameCount <= m_enabledFrame + 1) return;
		// mouse hover doesn't select so this is only controller/keyboard
		if (Interactable && !(eventData is PointerEventData)) Play(hover);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left) PlayClick();
	}

	public void OnSubmit(BaseEventData eventData) => PlayClick();

	private void PlayClick() => Play(Interactable ? click : clickWhileDisabled);

	private static void Play(EventReference sound)
	{
		if (!sound.IsNull) RAudio.PlayOneShot(sound);
	}
}
