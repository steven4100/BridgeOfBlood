using System;

namespace BridgeOfBlood.Data.Shared
{
	public enum CombatMetricProperty : byte
	{
		Hits,
		Kills,
		Crits,
		TotalDamage,
		PhysicalDamage,
		FireDamage,
		ColdDamage,
		LightningDamage,
		OverkillDamage,
		AttackEntitiesExpired,
		Duration,
		DPS
	}

	public static class CombatMetricResolver
	{
		public static float GetValue(CombatMetricProperty property, in CombatMetrics metrics)
		{
			return property switch
			{
				CombatMetricProperty.Hits => metrics.hits,
				CombatMetricProperty.Kills => metrics.kills,
				CombatMetricProperty.Crits => metrics.crits,
				CombatMetricProperty.TotalDamage => metrics.totalDamage,
				CombatMetricProperty.PhysicalDamage => metrics.physicalDamage,
				CombatMetricProperty.FireDamage => metrics.fireDamage,
				CombatMetricProperty.ColdDamage => metrics.coldDamage,
				CombatMetricProperty.LightningDamage => metrics.lightningDamage,
				CombatMetricProperty.OverkillDamage => metrics.overkillDamage,
				CombatMetricProperty.AttackEntitiesExpired => metrics.attackEntitiesExpired,
				CombatMetricProperty.Duration => metrics.duration,
				CombatMetricProperty.DPS => metrics.DPS,
				_ => throw new ArgumentOutOfRangeException(nameof(property), property, null)
			};
		}
	}
}
