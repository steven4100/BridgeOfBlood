using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[Serializable, MenuPath("Spell")]
	public class SpellModificationEffect : IEffect
	{
		public SpellModificationProperty property;
		public ModifierOperation operation;

		[SerializeReference, SerializeInterface]
		public IValue<float> value;

		public bool Apply(EffectContext context)
		{
			var modifier = new SpellModificationModifier
			{
				property = property,
				operation = operation,
				value = value.Resolve(context)
			};
			SpellModificationResolver.Apply(in modifier, context.spellModifications);
			return true;
		}
	}
}
