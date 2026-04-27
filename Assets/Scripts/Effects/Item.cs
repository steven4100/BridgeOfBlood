using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[CreateAssetMenu(fileName = "NewItem", menuName = "Bridge of Blood/Items/Joker")]
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
			context.Inventory.AddItem(this);
		}
	}

    [CreateAssetMenu(fileName = "NewSpellItem", menuName = "Bridge of Blood/Items/Spell Item")]
	public class SpellItem : Item
	{
		public SpellAttributeMaskCondition attributeMask;
		
		public bool CanApplyToSpell(RuntimeSpell spell){
			return attributeMask.Evaluate(spell);
		}

		public void OnAppliedToSpell(RuntimeSpell spell){
			foreach (var effect in effects){
				if(effect is ConditionalEffect conditionalEffect){
					conditionalEffect.conditions.Add(new RuntimeSpellCondition(spell));
				}
			}
		}
	}
}
