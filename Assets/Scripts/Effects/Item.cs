using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[CreateAssetMenu(fileName = "NewItem", menuName = "Bridge of Blood/Items/Joker")]
	public class Item : ScriptableObject, IEffect, IPurchasable
	{
		[SerializeField] ShopItemDefinition shopItemDefinition;

		[SerializeReference, SerializeInterface]
		public List<IEffect> effects;

		[Tooltip("Post-simulation combat reactions (on kill / ailment apply). Separate from per-frame IEffect modifiers.")]
		public List<CombatAttackSpawnReaction> combatReactions = new List<CombatAttackSpawnReaction>();

		public ShopItemDefinition ShopItemDefinition => shopItemDefinition;

		float IRandomElement.Weight
		{
			get => ((IRandomElement)shopItemDefinition).Weight;
			set => ((IRandomElement)shopItemDefinition).Weight = value;
		}

		public bool Apply(EffectContext context)
		{
			if (effects == null) return false;

			bool anyApplied = false;
			foreach (var effect in effects)
				anyApplied |= effect.Apply(context);

			return anyApplied;
		}

		public virtual bool CanPurchase(PurchaseContext context)
		{
			return context.Inventory.Stash.TryFindFirstFreeCell(out _);
		}

		public virtual void OnPurchase(PurchaseContext context)
		{
			Stash stash = context.Inventory.Stash;
			if (!stash.TryFindFirstFreeCell(out Vector2Int cell))
				return;
			stash.TryPlace(CreatePurchasedStashOccupant(), cell);
		}

		protected virtual IInventoryOccupant CreatePurchasedStashOccupant()
		{
			return new RuntimeItem(this);
		}
	}
}
