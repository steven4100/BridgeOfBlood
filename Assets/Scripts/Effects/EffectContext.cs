using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
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
}
