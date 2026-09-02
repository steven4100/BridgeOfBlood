using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(40)]
public class InventoryTransferCoordinator : MonoBehaviour
{
	public static InventoryTransferCoordinator Current { get; private set; }

	readonly List<RaycastResult> _raycastHits = new List<RaycastResult>();

	InventoryDragHandle _handle;
	IItemReceptacle _previewZone;
	RectTransform _ghost;
	Image _ghostImage;
	Canvas _ghostCanvas;

	void OnDestroy()
	{
		if (_ghostCanvas != null)
			Destroy(_ghostCanvas.gameObject);
	}

	void OnEnable()
	{
		if (Current == null)
			Current = this;
	}

	void OnDisable()
	{
		if (Current == this)
			Current = null;
	}

	public void BeginDrag(InventoryDragHandle handle, PointerEventData eventData)
	{
		_handle = handle;
		EnsureGhost();
		_ghostImage.sprite = handle.Occupant.GhostSprite;
		_ghostImage.enabled = true;
		_ghost.gameObject.SetActive(true);
		MoveGhost(eventData.position);
	}

	public void Drag(PointerEventData eventData)
	{
		if (_handle == null)
			return;
		MoveGhost(eventData.position);
		IItemReceptacle zone = ResolveReceptacle(eventData);
		UpdatePreview(zone, eventData);
	}

	public void EndDrag(PointerEventData eventData)
	{
		if (_handle == null)
			return;

		IItemReceptacle zone = ResolveReceptacle(eventData);
		if (zone != null)
			TryCommit(zone, eventData);

		ClearPreview();
		HideGhost();
		_handle = null;
	}

	bool TryCommit(IItemReceptacle zone, PointerEventData eventData)
	{
		IInventoryOccupant occupant = _handle.Occupant;
		IItemReceptacle source = _handle.Source;
		ReceptacleDropContext preview = BuildContext(eventData, false);
		if (!zone.Accept(occupant, ref preview))
			return false;

		ReceptacleDropContext sourceRestore = SourceRestoreContext();
		if (!source.TryRemove(occupant))
			return false;

		ReceptacleDropContext commit = preview;
		commit.Commit = true;
		commit.Relocating = null;
		if (zone.Accept(occupant, ref commit))
			return true;

		source.Accept(occupant, ref sourceRestore);
		return false;
	}

	ReceptacleDropContext BuildContext(PointerEventData eventData, bool commit)
	{
		return new ReceptacleDropContext
		{
			Commit = commit,
			UsePointer = true,
			Relocating = _handle.Occupant,
			SourceIndex = _handle.SourceIndex,
			ScreenPosition = eventData.position,
			EventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera,
		};
	}

	ReceptacleDropContext SourceRestoreContext()
	{
		return new ReceptacleDropContext
		{
			Commit = true,
			UsePointer = false,
			InsertIndex = _handle.SourceIndex,
			Cell = _handle.SourceCell,
		};
	}

	void UpdatePreview(IItemReceptacle zone, PointerEventData eventData)
	{
		if (!ReferenceEquals(_previewZone, zone))
		{
			_previewZone?.SetDropPreview(null);
			StashController.ClearFootprint();
			_previewZone = zone;
		}

		if (zone == null)
			return;

		ReceptacleDropContext ctx = BuildContext(eventData, false);
		bool valid = zone.Accept(_handle.Occupant, ref ctx);
		zone.SetDropPreview(valid);
	}

	void ClearPreview()
	{
		_previewZone?.SetDropPreview(null);
		_previewZone = null;
		StashController.ClearFootprint();
	}

	IItemReceptacle ResolveReceptacle(PointerEventData eventData)
	{
		if (EventSystem.current == null)
			return null;
		_raycastHits.Clear();
		EventSystem.current.RaycastAll(eventData, _raycastHits);
		for (int i = 0; i < _raycastHits.Count; i++)
		{
			GameObject go = _raycastHits[i].gameObject;
			if (_handle != null && go.transform.IsChildOf(_handle.transform))
				continue;
			IItemReceptacle zone = go.GetComponent<IItemReceptacle>();
			if (zone == null)
				zone = go.GetComponentInParent<IItemReceptacle>();
			if (zone != null)
				return zone;
		}
		return null;
	}

	void EnsureGhost()
	{
		if (_ghost != null)
			return;

		var canvasGo = new GameObject("InventoryDragGhostCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		_ghostCanvas = canvasGo.GetComponent<Canvas>();
		_ghostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_ghostCanvas.sortingOrder = 500;
		canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

		var ghostGo = new GameObject("Ghost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		_ghost = ghostGo.GetComponent<RectTransform>();
		_ghost.SetParent(canvasGo.transform, false);
		_ghost.sizeDelta = new Vector2(72f, 72f);
		_ghostImage = ghostGo.GetComponent<Image>();
		_ghostImage.raycastTarget = false;
		_ghostImage.preserveAspect = true;
		_ghost.gameObject.SetActive(false);
	}

	void MoveGhost(Vector2 screenPosition)
	{
		if (_ghost == null)
			return;
		_ghost.position = screenPosition;
	}

	void HideGhost()
	{
		if (_ghost != null)
			_ghost.gameObject.SetActive(false);
	}
}
