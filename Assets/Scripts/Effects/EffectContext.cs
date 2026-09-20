using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Telemetry;

namespace BridgeOfBlood.Effects
{
	public interface ITelemetryStatLookup
	{
		float GetTelemetry(MetricsScope scope, string key);
		float GetTelemetryForSpell(MetricsScope scope, string key, int spellId);
	}

	public class EffectContext
	{
		public ITelemetryStatLookup telemetry;
		public ModificationCatalog spellModificationCollection;
		/// <summary>Per-evaluation write sink; set to <see cref="ModificationCatalog.ForSpell"/> for the target slot.</summary>
		public ModificationSet spellModifications;
		public SpellInvocationContext spellInvocation;

		/// <summary>
		/// Combat telemetry for <paramref name="scope"/>. <see cref="MetricsScope.SpellCast"/> is the previous
		/// loop slot's per-spell totals this loop (forecast-safe). Slot 1 reads 0.
		/// The aggregator's live SpellCast store remains the current paid slot including recasts.
		/// </summary>
		public float GetTelemetry(MetricsScope scope, string key)
		{
			if (scope != MetricsScope.SpellCast)
				return telemetry.GetTelemetry(scope, key);

			int previousSlot = spellInvocation.spellSlotNumber - 1;
			int spellId = SpellInvocationResolver.GetSlotSpellId(in spellInvocation, previousSlot);
			if (spellId == 0)
				return 0f;
			return telemetry.GetTelemetryForSpell(MetricsScope.SpellLoop, key, spellId);
		}

		public float GetTelemetryDps(MetricsScope scope)
		{
			float duration = GetTelemetry(scope, TelemetryCoreStats.DurationKey);
			if (duration <= 0f)
				return 0f;
			return GetTelemetry(scope, TelemetryCoreStats.TotalDamageKey) / duration;
		}
	}
}
