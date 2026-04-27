using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Per-item drag forwarder for <see cref="HorizontalLayoutReorderGroup"/>.
/// Put on the same GameObject as a <see cref="Graphic"/> with <b>Raycast Target</b> enabled so the <see cref="EventSystem"/> delivers drag callbacks.
/// Optional <see cref="CanvasGroup"/> on this object: during drag, <see cref="CanvasGroup.blocksRaycasts"/> is cleared so the pointer can resolve gaps between siblings.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class HorizontalLayoutReorderHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[SerializeField]
	[Tooltip("If unset, uses a CanvasGroup on this GameObject when present.")]
	CanvasGroup dragCanvasGroup;
	[SerializeField]
	[Range(0f, 1f)]
	float dragAlpha = 0.65f;

	HorizontalLayoutReorderGroup _group;
	RectTransform _rect;
	float _savedAlpha = 1f;
	bool _savedBlocksRaycasts = true;
	bool _modifiedCanvasGroup;

	void Awake()
	{
		_rect = (RectTransform)transform;
		_group = GetComponentInParent<HorizontalLayoutReorderGroup>();
		if (dragCanvasGroup == null)
			dragCanvasGroup = GetComponent<CanvasGroup>();
	}

	void OnEnable()
	{
		if (_group == null)
			_group = GetComponentInParent<HorizontalLayoutReorderGroup>();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (dragCanvasGroup != null)
		{
			_savedAlpha = dragCanvasGroup.alpha;
			_savedBlocksRaycasts = dragCanvasGroup.blocksRaycasts;
			dragCanvasGroup.alpha = dragAlpha;
			dragCanvasGroup.blocksRaycasts = false;
			_modifiedCanvasGroup = true;
		}
		else
			_modifiedCanvasGroup = false;

		_group.NotifyDrag(_rect, eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		_group.NotifyDrag(_rect, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (_modifiedCanvasGroup && dragCanvasGroup != null)
		{
			dragCanvasGroup.alpha = _savedAlpha;
			dragCanvasGroup.blocksRaycasts = _savedBlocksRaycasts;
		}

		_group.NotifyEndDrag(_rect);
	}
}
