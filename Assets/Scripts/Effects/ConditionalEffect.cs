using BridgeOfBlood.Data.Shared;
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
	}
}
