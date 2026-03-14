using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

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

	public class EffectContext
	{
		public CombatMetrics frameMetrics;
		public CombatMetrics spellCastMetrics;
		public CombatMetrics spellLoopMetrics;
		public CombatMetrics roundMetrics;
		public CombatMetrics gameMetrics;
		public SpellModifications spellModifications;

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
