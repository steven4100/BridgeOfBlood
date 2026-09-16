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
		public SpellModificationCollection spellModificationCollection;
		/// <summary>Per-evaluation write sink; set to <see cref="SpellModificationCollection.ForSpell"/> for the target slot.</summary>
		public SpellModifications spellModifications;
		public SpellInvocationContext spellInvocation;

		public float GetTelemetry(MetricsScope scope, string key)
		{
			if (scope == MetricsScope.SpellCast)
				return GetSpellCastTelemetry(key);
			return telemetry.GetTelemetry(scope, key);
		}

		public float GetTelemetryDps(MetricsScope scope)
		{
			float duration = GetTelemetry(scope, TelemetryCoreStats.DurationKey);
			if (duration <= 0f)
				return 0f;
			return GetTelemetry(scope, TelemetryCoreStats.TotalDamageKey) / duration;
		}

		float GetSpellCastTelemetry(string key)
		{
			int oneBasedSlot = spellInvocation.spellSlotNumber;
			var spells = spellInvocation.spells;
			if (spells == null || oneBasedSlot <= 1 || oneBasedSlot - 1 > spells.Count)
				return 0f;
			int spellId = spells[oneBasedSlot - 2].spellId;
			return telemetry.GetTelemetryForSpell(MetricsScope.SpellLoop, key, spellId);
		}
	}
}
