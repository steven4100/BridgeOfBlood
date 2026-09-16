using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Telemetry;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class ValueCondition : ICondition
	{
		[SerializeReference, SerializeInterface]
		public IValue<float> lhs;

		public Comparison comparison;

		[SerializeReference, SerializeInterface]
		public IValue<float> rhs;

		public bool Evaluate(EffectContext context)
		{
			return ConditionEvaluator.Compare(lhs.Resolve(context), comparison, rhs.Resolve(context));
		}

		public string GetDescription()
		{
			if (lhs is SlotAttributeCheckValue slotCheck
				&& rhs is ConstantValue constant
				&& comparison == Comparison.Equal
				&& (Mathf.Approximately(constant.value, 0f) || Mathf.Approximately(constant.value, 1f)))
			{
				return EffectDescription.SlotAttributeCheck(
					slotCheck.slot,
					slotCheck.attributeFilter,
					negated: Mathf.Approximately(constant.value, 0f));
			}

			return $"{lhs.GetDescription()} {EffectDescription.ComparisonWord(comparison)} {rhs.GetDescription()}";
		}

		public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination)
		{
			lhs.CollectTelemetrySubscriptions(destination);
			rhs.CollectTelemetrySubscriptions(destination);
		}
	}
}
