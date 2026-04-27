using System;
using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	public static class ConditionEvaluator
	{
		public static bool Compare(float a, Comparison op, float b) => op switch
		{
			Comparison.GreaterThan => a > b,
			Comparison.LessThan => a < b,
			Comparison.Equal => Mathf.Approximately(a, b),
			Comparison.NotEqual => !Mathf.Approximately(a, b),
			Comparison.GreaterThanOrEqual => a >= b,
			Comparison.LessThanOrEqual => a <= b,
			_ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
		};
	}
}
