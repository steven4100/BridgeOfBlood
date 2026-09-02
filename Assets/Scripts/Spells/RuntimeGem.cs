using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	public sealed class RuntimeGem : IInventoryOccupant
	{
		public SpellItem Definition { get; }

		public int OccupancyCount => 1;
		public int TileSideLength => 1;
		public Sprite GhostSprite => Definition.ShopItemDefinition.Sprite;

		public RuntimeGem(SpellItem definition)
		{
			Definition = definition;
		}
	}
}
