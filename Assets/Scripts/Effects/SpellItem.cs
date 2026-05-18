using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
    [CreateAssetMenu(fileName = "NewSpellItem", menuName = "Bridge of Blood/Items/Spell Item")]
    public class SpellItem : Item, ISpellTargetPurchasable
    {
        public SpellAttributeMaskCondition attributeMask;

        public bool CanApplyToSpell(RuntimeSpell spell)
        {
            return attributeMask.Evaluate(spell);
        }

        public void OnAppliedToSpell(RuntimeSpell spell)
        {
            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                IEffect effect = effects[i];
                if (effect is ConditionalEffect conditionalEffect)
                {
                    if (conditionalEffect.conditions == null)
                        conditionalEffect.conditions = new List<ICondition>();
                    conditionalEffect.conditions.Add(new RuntimeSpellCondition(spell));
                    continue;
                }

                effects[i] = WrapEffectWithSpellCondition(effect, spell);
            }
        }

        static ConditionalEffect WrapEffectWithSpellCondition(IEffect inner, RuntimeSpell spell)
        {
            return new ConditionalEffect
            {
                conditions = new List<ICondition> { new RuntimeSpellCondition(spell) },
                effects = new List<IEffect> { inner },
            };
        }

        bool ISpellTargetPurchasable.CanBeApplied(RuntimeSpell spell)
        {
            return CanApplyToSpell(spell);
        }

        bool ISpellTargetPurchasable.PurchaseAndApplyToSpell(RuntimeSpell spell)
        {
            return true;
        }

        public override void OnPurchase(PurchaseContext context)
        {
            RuntimeSpell target = context.SpellGemTarget;
            if (target != null)
            {
                target.AddRuntimeSpellItem(new RuntimeSpellItem { spellItem = this });
                OnAppliedToSpell(target);
                ((ISpellTargetPurchasable)this).PurchaseAndApplyToSpell(target);
                context.SpellInventory.NotifySpellsChanged();
                return;
            }

            base.OnPurchase(context);
        }
    }
}
