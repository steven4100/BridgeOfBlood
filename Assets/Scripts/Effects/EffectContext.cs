using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	public class EffectContext
	{
		public CombatMetrics frameMetrics;
		/// <summary>
		/// Per-loop-slot metrics for the current spell loop, aligned with <see cref="SpellInvocationContext.spells"/>.
		/// Indexed by simulated <c>spellSlotNumber</c> for <see cref="MetricsScope.SpellCast"/>.
		/// </summary>
		public List<CombatMetrics> spellCastMetrics = new List<CombatMetrics>();

		public CombatMetrics spellLoopMetrics;
		public CombatMetrics roundMetrics;
		public CombatMetrics gameMetrics;
		public SpellModificationCollection spellModificationCollection;
		/// <summary>Per-evaluation write sink; set to <see cref="SpellModificationCollection.ForSpell"/> for the target slot.</summary>
		public SpellModifications spellModifications;
		public SpellInvocationContext spellInvocation;

		public CombatMetrics GetMetrics(MetricsScope scope) => scope switch
		{
			MetricsScope.Frame => frameMetrics,
			MetricsScope.SpellCast => GetSpellCastMetricsForSimulatedSlot(),
			MetricsScope.SpellLoop => spellLoopMetrics,
			MetricsScope.Round => roundMetrics,
			MetricsScope.Game => gameMetrics,
			_ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
		};

		CombatMetrics GetSpellCastMetricsForSimulatedSlot()
		{
			int oneBasedSlot = spellInvocation.spellSlotNumber;
			if (oneBasedSlot <= 1 || oneBasedSlot > spellCastMetrics.Count)
				return default;
			return spellCastMetrics[oneBasedSlot - 2];
		}
	}
}
