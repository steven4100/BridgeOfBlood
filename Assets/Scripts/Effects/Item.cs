using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[CreateAssetMenu(fileName = "NewItem", menuName = "Bridge of Blood/Item")]
	public class Item : ScriptableObject, IEffect, IPurchasable, IInventoryItem
	{
		[SerializeField] ShopItemDefinition shopItemDefinition;

		[SerializeReference, SerializeInterface]
		public List<IEffect> effects;

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

		public void OnPurchase(PurchaseContext context)
		{
			context.AddPurchasedPayload(this);
		}
	}
}
