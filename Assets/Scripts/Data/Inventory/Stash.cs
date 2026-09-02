using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	public sealed class Stash
	{
		readonly List<StashPlacement> _placements = new List<StashPlacement>();
		readonly IInventoryOccupant[] _cells;

		public int Width { get; }
		public int Height { get; }
		public IReadOnlyList<StashPlacement> Placements => _placements;

		public event Action StashUpdated;

		public Stash(int width, int height)
		{
			Width = Mathf.Max(1, width);
			Height = Mathf.Max(1, height);
			_cells = new IInventoryOccupant[Width * Height];
		}

		public bool TryGetOrigin(IInventoryOccupant occupant, out Vector2Int origin)
		{
			for (int i = 0; i < _placements.Count; i++)
			{
				if (!ReferenceEquals(_placements[i].Occupant, occupant))
					continue;
				origin = _placements[i].Origin;
				return true;
			}
			origin = default;
			return false;
		}

		public bool TryFindFirstFreeCell(out Vector2Int cell)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (_cells[Index(x, y)] == null)
					{
						cell = new Vector2Int(x, y);
						return true;
					}
				}
			}
			cell = default;
			return false;
		}

		public IInventoryOccupant OccupantAt(Vector2Int cell)
		{
			if (!InBounds(cell))
				return null;
			return _cells[Index(cell.x, cell.y)];
		}

		public bool HasSpace(int itemCount, int tileSideLength, Vector2Int origin, IInventoryOccupant relocating)
		{
			if (itemCount < 1 || tileSideLength < 1)
				return false;
			if (!CanFitSquare(origin, tileSideLength, relocating))
				return false;

			int freeSquares = CountFreeSquares(tileSideLength, relocating);
			return freeSquares >= itemCount;
		}

		public bool TryPlace(IInventoryOccupant item, Vector2Int cell)
		{
			int side = item.TileSideLength;
			if (!CanFitSquare(cell, side, null))
				return false;

			Occupy(item, cell, side);
			_placements.Add(new StashPlacement(item, cell, side));
			StashUpdated?.Invoke();
			return true;
		}

		public bool TryFindExtraCells(
			Vector2Int reservedOrigin,
			int reservedSide,
			int extraCount,
			IInventoryOccupant relocating,
			List<Vector2Int> extraCells)
		{
			extraCells.Clear();
			if (!CanFitSquare(reservedOrigin, reservedSide, relocating))
				return false;
			CollectFreeCellsExcept(reservedOrigin, reservedSide, extraCount, relocating, extraCells);
			return extraCells.Count >= extraCount;
		}

		public void Clear()
		{
			Array.Clear(_cells, 0, _cells.Length);
			_placements.Clear();
			StashUpdated?.Invoke();
		}

		public bool OwnsPayload(ScriptableObject asset)
		{
			for (int i = 0; i < _placements.Count; i++)
			{
				IInventoryOccupant occupant = _placements[i].Occupant;
				if (occupant is RuntimeSpell spell && ReferenceEquals(spell.Definition, asset))
					return true;
				if (occupant is RuntimeGem gem && ReferenceEquals(gem.Definition, asset))
					return true;
				if (occupant is RuntimeItem item && ReferenceEquals(item.Definition, asset))
					return true;
			}
			return false;
		}

		void CollectFreeCellsExcept(
			Vector2Int reservedOrigin,
			int side,
			int count,
			IInventoryOccupant relocating,
			List<Vector2Int> into)
		{
			for (int y = 0; y <= Height - side && into.Count < count; y++)
			{
				for (int x = 0; x <= Width - side && into.Count < count; x++)
				{
					if (x == reservedOrigin.x && y == reservedOrigin.y)
						continue;
					var cell = new Vector2Int(x, y);
					if (CanFitSquare(cell, side, relocating))
						into.Add(cell);
				}
			}
		}

		public bool TryRemove(IInventoryOccupant occupant)
		{
			for (int i = 0; i < _placements.Count; i++)
			{
				if (!ReferenceEquals(_placements[i].Occupant, occupant))
					continue;

				StashPlacement placement = _placements[i];
				ClearSquare(placement.Origin, placement.Side);
				_placements.RemoveAt(i);
				StashUpdated?.Invoke();
				return true;
			}
			return false;
		}

		int Index(int x, int y) => y * Width + x;

		bool InBounds(Vector2Int cell)
		{
			return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
		}

		bool CellIsFreeFor(int x, int y, IInventoryOccupant relocating)
		{
			IInventoryOccupant occupant = _cells[Index(x, y)];
			return occupant == null || ReferenceEquals(occupant, relocating);
		}

		bool CanFitSquare(Vector2Int origin, int side, IInventoryOccupant relocating)
		{
			if (origin.x < 0 || origin.y < 0)
				return false;
			if (origin.x + side > Width || origin.y + side > Height)
				return false;
			for (int y = 0; y < side; y++)
			{
				for (int x = 0; x < side; x++)
				{
					if (!CellIsFreeFor(origin.x + x, origin.y + y, relocating))
						return false;
				}
			}
			return true;
		}

		int CountFreeSquares(int side, IInventoryOccupant relocating)
		{
			int count = 0;
			for (int y = 0; y <= Height - side; y++)
			{
				for (int x = 0; x <= Width - side; x++)
				{
					if (CanFitSquare(new Vector2Int(x, y), side, relocating))
						count++;
				}
			}
			return count;
		}

		void Occupy(IInventoryOccupant item, Vector2Int origin, int side)
		{
			for (int y = 0; y < side; y++)
			{
				for (int x = 0; x < side; x++)
					_cells[Index(origin.x + x, origin.y + y)] = item;
			}
		}

		void ClearSquare(Vector2Int origin, int side)
		{
			for (int y = 0; y < side; y++)
			{
				for (int x = 0; x < side; x++)
					_cells[Index(origin.x + x, origin.y + y)] = null;
			}
		}
	}
}
