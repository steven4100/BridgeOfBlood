using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	public sealed class RuntimeItem : IInventoryOccupant
	{
		public Item Definition { get; }

		public int OccupancyCount => 1;
		public int TileSideLength => 1;
		public Sprite GhostSprite => Definition.ShopItemDefinition.Sprite;

		public RuntimeItem(Item definition)
		{
			Definition = definition;
		}
	}
}
