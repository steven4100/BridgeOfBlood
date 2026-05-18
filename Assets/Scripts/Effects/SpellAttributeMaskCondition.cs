using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using System;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class SpellAttributeMaskCondition : ICondition
	{
		public SpellAttributeMask attributeMask;

		public bool Evaluate(RuntimeSpell spell)
		{
			return (spell.Definition.attributeMask & attributeMask) != 0;
		}
		public bool Evaluate(EffectContext context)
		{
			return (context.spellInvocation.spells[context.spellInvocation.spellSlotNumber].Definition.attributeMask & attributeMask) != 0;
		}
	}
}
