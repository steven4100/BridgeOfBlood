using System;
using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class CombatMetricCondition : ICondition
	{
		public MetricsScope scope;
		public CombatMetricProperty property;
		public Comparison comparison;
		public float value;

		public bool Evaluate(EffectContext context)
		{
			var metrics = context.GetMetrics(scope);
			float actual = CombatMetricResolver.GetValue(property, in metrics);
			return ConditionEvaluator.Compare(actual, comparison, value);
		}
	}
}
