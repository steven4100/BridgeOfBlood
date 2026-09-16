using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Telemetry;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class ConditionalEffect : IEffect
	{
		[SerializeReference, SerializeInterface]
		public List<ICondition> conditions;

		[SerializeReference, SerializeInterface]
		public List<IEffect> effects;

		public bool Apply(EffectContext context)
		{
			if (conditions != null)
				foreach (var c in conditions)
					if (!c.Evaluate(context))
						return false;

			bool anyApplied = false;
			if (effects != null)
				foreach (var effect in effects)
					anyApplied |= effect.Apply(context);

			return anyApplied;
		}

		public string GetDescription()
		{
			string then = EffectDescription.JoinThen(effects);
			string when = EffectDescription.JoinAnd(conditions);
			if (string.IsNullOrEmpty(when))
				return then;
			if (string.IsNullOrEmpty(then))
				return $"If {when}";
			return $"If {when}, then {then}";
		}

		public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination)
		{
			if (conditions != null)
			{
				for (int i = 0; i < conditions.Count; i++)
					conditions[i].CollectTelemetrySubscriptions(destination);
			}

			if (effects != null)
			{
				for (int i = 0; i < effects.Count; i++)
					effects[i].CollectTelemetrySubscriptions(destination);
			}
		}
	}
}
