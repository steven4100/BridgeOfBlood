using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	public readonly struct StashPlacement
	{
		public readonly IInventoryOccupant Occupant;
		public readonly Vector2Int Origin;
		public readonly int Side;

		public StashPlacement(IInventoryOccupant occupant, Vector2Int origin, int side)
		{
			Occupant = occupant;
			Origin = origin;
			Side = side;
		}
	}
}
