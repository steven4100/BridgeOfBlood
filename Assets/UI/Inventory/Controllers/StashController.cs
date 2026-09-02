using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
public class StashController : MonoBehaviour
{
	public static StashController Current { get; private set; }

	const float CellSize = 40f;

	Stash _stash;
	readonly List<StashCellPresenter> _cells = new List<StashCellPresenter>();
	readonly List<Vector2Int> _extraCells = new List<Vector2Int>();
	Image[] _cellImages;

	void OnEnable()
	{
		Current = this;
		ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
	}

	void OnDisable()
	{
		ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
		if (_stash != null)
			_stash.StashUpdated -= Render;
		if (Current == this)
			Current = null;
	}

	void OnServicesRegistered(ref ServicesRegisteredEvent _)
	{
		IInventoryService service = ServiceLocator.Current.GetService<IInventoryService>();
		if (_stash != null)
			_stash.StashUpdated -= Render;
		_stash = service.Stash;
		_stash.StashUpdated += Render;
		BuildGrid(_stash.Width, _stash.Height);
		Render();
	}

	void BuildGrid(int width, int height)
	{
		for (int i = 0; i < _cells.Count; i++)
			Destroy(_cells[i].gameObject);
		_cells.Clear();

		var grid = GetComponent<GridLayoutGroup>();
		if (grid == null)
			grid = gameObject.AddComponent<GridLayoutGroup>();
		grid.cellSize = new Vector2(CellSize, CellSize);
		grid.spacing = new Vector2(2f, 2f);
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		grid.constraintCount = width;
		grid.childAlignment = TextAnchor.LowerLeft;

		var fitter = GetComponent<ContentSizeFitter>();
		if (fitter == null)
			fitter = gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		int total = width * height;
		_cellImages = new Image[total];
		for (int i = 0; i < total; i++)
		{
			int x = i % width;
			int y = i / width;
			var cellGo = new GameObject($"StashCell_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			cellGo.transform.SetParent(transform, false);
			var image = cellGo.GetComponent<Image>();
			image.color = CellBaseColor;
			image.raycastTarget = true;
			var presenter = cellGo.AddComponent<StashCellPresenter>();
			_cells.Add(presenter);
			_cellImages[i] = image;
		}
	}

	void Render()
	{
		if (_stash == null)
			return;

		for (int i = 0; i < _cells.Count; i++)
		{
			int x = i % _stash.Width;
			int y = i / _stash.Width;
			var cell = new Vector2Int(x, y);
			IInventoryOccupant occupant = _stash.OccupantAt(cell);
			bool origin = occupant != null && _stash.TryGetOrigin(occupant, out Vector2Int originCell) && originCell == cell;
			_cellImages[i].color = occupant != null ? new Color(0.75f, 0.75f, 0.85f, 1f) : CellBaseColor;
			_cellImages[i].sprite = origin ? occupant.GhostSprite : null;
			_cells[i].Bind(this, cell, origin ? occupant : null);
		}
	}

	static readonly Color CellBaseColor = new Color(0.2f, 0.2f, 0.22f, 0.85f);

	public bool VisitSpellAt(RuntimeSpell spell, Vector2Int cell, ref ReceptacleDropContext ctx)
	{
		ctx.Cell = cell;
		IInventoryOccupant relocating = ctx.Commit ? null : ctx.Relocating;
		bool can = _stash.TryFindExtraCells(cell, spell.TileSideLength, spell.Gems.FilledCount, relocating, _extraCells);
		if (!ctx.Commit)
		{
			ShowFootprint(cell, spell.OccupancyCount, can);
			return can;
		}
		if (!can)
			return false;

		var gemCells = new Vector2Int[_extraCells.Count];
		_extraCells.CopyTo(gemCells);
		if (!_stash.TryPlace(spell, cell))
			return false;
		List<RuntimeGem> gems = spell.Gems.ExtractAll();
		for (int i = 0; i < gems.Count; i++)
			_stash.TryPlace(gems[i], gemCells[i]);
		return true;
	}

	public bool VisitOccupantAt(IInventoryOccupant occupant, Vector2Int cell, ref ReceptacleDropContext ctx)
	{
		ctx.Cell = cell;
		IInventoryOccupant relocating = ctx.Commit ? null : ctx.Relocating;
		bool can = _stash.HasSpace(occupant.OccupancyCount, occupant.TileSideLength, cell, relocating);
		if (!ctx.Commit)
		{
			ShowFootprint(cell, occupant.OccupancyCount, can);
			return can;
		}
		return can && _stash.TryPlace(occupant, cell);
	}

	public bool TryRemove(IInventoryOccupant occupant)
	{
		return _stash.TryRemove(occupant);
	}

	public static void ShowFootprint(Vector2Int origin, int count, bool valid)
	{
		StashController controller = Current;
		if (controller == null || controller._stash == null)
			return;

		Color tint = valid ? new Color(0.45f, 0.9f, 0.45f, 0.9f) : new Color(0.9f, 0.35f, 0.35f, 0.9f);
		int width = controller._stash.Width;
		int start = origin.y * width + origin.x;
		for (int n = 0; n < count; n++)
		{
			int i = start + n;
			if (i < 0 || i >= controller._cellImages.Length)
				break;
			controller._cellImages[i].color = tint;
		}
	}

	public static void ClearFootprint()
	{
		Current?.Render();
	}
}

public class StashCellPresenter : MonoBehaviour, IItemReceptacle
{
	StashController _owner;
	Vector2Int _cell;
	Image _image;
	Color _dropPreviewBase;

	public void Bind(StashController owner, Vector2Int cell, IInventoryOccupant occupant)
	{
		_owner = owner;
		_cell = cell;
		if (_image == null)
			_image = GetComponent<Image>();
		_dropPreviewBase = _image.color;
		if (occupant != null)
			InventoryDragHandle.Bind(gameObject, occupant, this, -1, cell);
		else
			InventoryDragHandle.Clear(gameObject);
	}

	public void SetDropPreview(bool? valid)
	{
		InventoryDropPreview.Apply(_image, ref _dropPreviewBase, valid);
	}

	Vector2Int DropCell(in ReceptacleDropContext ctx)
	{
		return ctx.UsePointer ? _cell : ctx.Cell;
	}

	public bool VisitSpell(RuntimeSpell spell, ref ReceptacleDropContext ctx)
	{
		return _owner.VisitSpellAt(spell, DropCell(ctx), ref ctx);
	}

	public bool VisitGem(RuntimeGem gem, ref ReceptacleDropContext ctx)
	{
		return _owner.VisitOccupantAt(gem, DropCell(ctx), ref ctx);
	}

	public bool VisitItem(RuntimeItem item, ref ReceptacleDropContext ctx)
	{
		return _owner.VisitOccupantAt(item, DropCell(ctx), ref ctx);
	}

	public bool TryRemove(IInventoryOccupant occupant)
	{
		return _owner.TryRemove(occupant);
	}
}
