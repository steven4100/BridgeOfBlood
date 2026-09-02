using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	public interface IInventoryOccupant
	{
		int OccupancyCount { get; }
		int TileSideLength { get; }
		Sprite GhostSprite { get; }
	}
}
