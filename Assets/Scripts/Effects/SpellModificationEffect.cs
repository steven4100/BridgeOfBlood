using System;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class SpellModificationEffect : IEffect
	{
		public SpellModificationModifier modifier;

		public bool Apply(EffectContext context)
		{
			SpellModificationResolver.Apply(in modifier, context.spellModifications);
			return true;
		}
	}
}
