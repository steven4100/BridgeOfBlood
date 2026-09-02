using BridgeOfBlood.Data.Inventory;
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
            RuntimeGem gem = new RuntimeGem(this);
            RuntimeSpell target = context.SpellGemTarget;
            if (target != null)
            {
                int slot = target.Gems.FirstEmptySlot;
                if (slot >= 0)
                {
                    target.Gems.TryInsert(gem, slot);
                    context.SpellInventory.NotifySpellsChanged();
                    return;
                }
            }

            Stash stash = context.Inventory.Stash;
            if (!stash.TryFindFirstFreeCell(out Vector2Int cell))
                return;
            stash.TryPlace(gem, cell);
        }
    }
}
