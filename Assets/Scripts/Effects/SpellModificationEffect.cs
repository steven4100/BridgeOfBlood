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
		[SerializeReference, SerializeInterface]
		public IModificationData modifier;

		public bool Apply(EffectContext context)
		{
			context.spellModifications.Add(modifier.Bake(context));
			return true;
		}

		public string GetDescription() => modifier.GetDescription();

		public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) =>
			modifier.CollectTelemetrySubscriptions(destination);
	}
}
