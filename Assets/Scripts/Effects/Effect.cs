using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MenuPathAttribute : Attribute
{
	public string Path { get; }
	public MenuPathAttribute(string path) => Path = path;
}

namespace BridgeOfBlood.Effects
{
	public interface ICondition
	{
		bool Evaluate(EffectContext context);
	}

	public interface IEffect
	{
		bool Apply(EffectContext context);
	}

	public enum MetricsScope : byte
	{
		Frame,
		SpellCast,
		SpellLoop,
		Round,
		Game
	}

	public struct SpellInvocationContext
	{
		public int totalSpellsCasted;
		public int spellLoopNumber;
		public int spellSlotNumber;
		public int spellLoopSlotCount;
		public int spellLoopsPerRound;
		public IReadOnlyList<RuntimeSpell> spells;
	}

	public class EffectContext
	{
		public CombatMetrics frameMetrics;
		public CombatMetrics spellCastMetrics;
		public CombatMetrics spellLoopMetrics;
		public CombatMetrics roundMetrics;
		public CombatMetrics gameMetrics;
		public SpellModifications spellModifications;
		public SpellInvocationContext spellInvocation;

		public CombatMetrics GetMetrics(MetricsScope scope) => scope switch
		{
			MetricsScope.Frame => frameMetrics,
			MetricsScope.SpellCast => spellCastMetrics,
			MetricsScope.SpellLoop => spellLoopMetrics,
			MetricsScope.Round => roundMetrics,
			MetricsScope.Game => gameMetrics,
			_ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
		};
	}

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

public enum ScheduleType
{
	Game,
	Round,
	SpellLoop,
	NextSpell,
}

public interface ISchedulable
{
	public ScheduleType scheduleType { get; }

	public void OnExpired();
}
