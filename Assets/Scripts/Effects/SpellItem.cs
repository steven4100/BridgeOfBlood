using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
    [CreateAssetMenu(fileName = "NewSpellItem", menuName = "Bridge of Blood/Items/Spell Item")]
    public class Gem : Item, ISpellTargetPurchasable
    {
        public SpellAttributeMaskCondition attributeMask;

        public bool CanApplyToSpell(RuntimeSpell spell)
        {
            return attributeMask.Evaluate(spell);
        }

        public override string GetDescription()
        {
            string body = base.GetDescription();
            if (EffectDescription.IsAllAttributes(attributeMask.attributeMask))
                return body;

            string applies = $"Applies to {EffectDescription.AttributeAdjectives(attributeMask.attributeMask)} spells.";
            if (string.IsNullOrEmpty(body))
                return applies;
            return applies + "\n" + body;
        }

        bool ISpellTargetPurchasable.CanBeApplied(RuntimeSpell spell)
        {
            return CanApplyToSpell(spell);
        }

        bool ISpellTargetPurchasable.PurchaseAndApplyToSpell(RuntimeSpell spell)
        {
            return true;
        }

        protected override IInventoryOccupant CreatePurchasedStashOccupant()
        {
            return new RuntimeGem(this);
        }
    }
}
