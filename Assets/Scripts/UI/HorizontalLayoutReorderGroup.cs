using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// uGUI strip that reorders immediate children by sibling index from pointer X while dragging.
/// Requires a <see cref="Canvas"/> with <see cref="GraphicRaycaster"/>, a scene <see cref="EventSystem"/>,
/// and a <see cref="Graphic"/> with <c>Raycast Target</c> on each <see cref="HorizontalLayoutReorderHandle"/> item.
/// If this strip is inside a <see cref="ScrollRect"/>, horizontal scrolling can fight drags; prefer a small dedicated drag handle graphic.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(HorizontalLayoutGroup))]
public class HorizontalLayoutReorderGroup : MonoBehaviour
{
	[SerializeField]
	[Tooltip("If false, inactive children are ignored when choosing the insert index from the pointer.")]
	bool includeInactiveChildren;

	RectTransform _strip;

	public event Action SiblingOrderChanged;
	public event Action ReorderEndDrag;

	void Awake()
	{
		_strip = (RectTransform)transform;
	}

	/// <summary>Updates the dragged item’s sibling index from <paramref name="eventData"/>.position.</summary>
	public void NotifyDrag(RectTransform item, PointerEventData eventData)
	{
		Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_strip, eventData.position, cam, out Vector2 localPoint))
			return;

		int newIndex = ComputeSiblingIndexFromLocalX(localPoint.x);
		if (item.GetSiblingIndex() != newIndex)
		{
			item.SetSiblingIndex(newIndex);
			SiblingOrderChanged?.Invoke();
		}
	}

	public void NotifyEndDrag(RectTransform item)
	{
		ReorderEndDrag?.Invoke();
	}

	int ComputeSiblingIndexFromLocalX(float localX)
	{
		int count = _strip.childCount;
		int newIndex = 0;
		for (int i = 0; i < count; i++)
		{
			var child = _strip.GetChild(i) as RectTransform;
			if (child == null)
				continue;
			if (!includeInactiveChildren && !child.gameObject.activeInHierarchy)
				continue;

			Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(_strip, child);
			if (localX > b.center.x)
				newIndex = i + 1;
		}

		return Mathf.Clamp(newIndex, 0, count - 1);
	}
}
