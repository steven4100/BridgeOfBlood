using BridgeOfBlood.Data.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	public IInventoryOccupant Occupant { get; private set; }
	public IItemReceptacle Source { get; private set; }
	public int SourceIndex { get; private set; }
	public Vector2Int SourceCell { get; private set; }

	CanvasGroup _canvasGroup;
	float _savedAlpha = 1f;
	bool _savedBlocksRaycasts = true;
	bool _dragging;

	public static InventoryDragHandle Bind(
		GameObject host,
		IInventoryOccupant occupant,
		IItemReceptacle source,
		int sourceIndex,
		Vector2Int sourceCell)
	{
		var reorder = host.GetComponent<HorizontalLayoutReorderHandle>();
		if (reorder != null)
			reorder.enabled = false;

		InventoryDragHandle handle = host.GetComponent<InventoryDragHandle>();
		if (handle == null)
			handle = host.AddComponent<InventoryDragHandle>();
		handle.enabled = occupant != null && source != null;
		handle.Occupant = occupant;
		handle.Source = source;
		handle.SourceIndex = sourceIndex;
		handle.SourceCell = sourceCell;
		handle.EnsureCanvasGroup();
		return handle;
	}

	public static void Clear(GameObject host)
	{
		InventoryDragHandle handle = host.GetComponent<InventoryDragHandle>();
		if (handle == null)
			return;
		handle.Occupant = null;
		handle.Source = null;
		handle.enabled = false;
	}

	void EnsureCanvasGroup()
	{
		_canvasGroup = GetComponent<CanvasGroup>();
		if (_canvasGroup == null)
			_canvasGroup = gameObject.AddComponent<CanvasGroup>();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (Occupant == null || Source == null || InventoryTransferCoordinator.Current == null)
			return;
		EnsureCanvasGroup();
		_savedAlpha = _canvasGroup.alpha;
		_savedBlocksRaycasts = _canvasGroup.blocksRaycasts;
		_canvasGroup.alpha = 0.45f;
		_canvasGroup.blocksRaycasts = false;
		_dragging = true;
		InventoryTransferCoordinator.Current.BeginDrag(this, eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!_dragging || InventoryTransferCoordinator.Current == null)
			return;
		InventoryTransferCoordinator.Current.Drag(eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!_dragging)
			return;
		_dragging = false;
		if (InventoryTransferCoordinator.Current != null)
			InventoryTransferCoordinator.Current.EndDrag(eventData);
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = _savedAlpha;
			_canvasGroup.blocksRaycasts = _savedBlocksRaycasts;
		}
	}
}
