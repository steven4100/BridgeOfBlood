using System;
using BridgeOfBlood.Data.Shared;
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
	}
}
