using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Telemetry;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[Serializable, MenuPath("Spell")]
	public class SpellModificationEffect : IEffect
	{
		// ParameterModifier contains SerializeReference IValue<float> fields; Unity requires this field to be SerializeReference too.
		// SerializeReference fields start null unless assigned; default instance so new effects show modifier in the inspector.
		[SerializeReference]
		public ParameterModifier modifier = new ParameterModifier();

		public bool Apply(EffectContext context)
		{
			context.spellModifications.Add(Bake(modifier, context));
			return true;
		}

		public string GetDescription() => EffectDescription.Describe(modifier);

		public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination)
		{
			modifier.flatAdditive?.CollectTelemetrySubscriptions(destination);
			modifier.percentIncreased?.CollectTelemetrySubscriptions(destination);
			modifier.moreMultiplier?.CollectTelemetrySubscriptions(destination);
		}

		public static ParameterModifier Bake(ParameterModifier source, EffectContext context)
		{
			var baked = new ParameterModifier
			{
				property = source.property,
				filter = source.filter,
			};

			if (source.flatAdditive != null)
				baked.flatAdditive = new ConstantValue { value = source.flatAdditive.Resolve(context) };
			if (source.percentIncreased != null)
				baked.percentIncreased = new ConstantValue { value = source.percentIncreased.Resolve(context) };
			if (source.moreMultiplier != null)
				baked.moreMultiplier = new ConstantValue { value = source.moreMultiplier.Resolve(context) };

			return baked;
		}
	}
}
