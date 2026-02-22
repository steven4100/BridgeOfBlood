using Unity.Collections;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Game context for predicate evaluation.
	/// Aggregates combat telemetry and game state for Event → Condition → Effect processing.
	/// 
	/// Note: Per-spell metrics are stored in NativeHashMap managed by systems.
	/// This struct contains aggregate totals and references to detailed tracking.
	/// </summary>
	public struct GameContext
	{
		// Aggregate combat telemetry
		public int totalEnemiesHit;
		public int totalEnemiesKilled;
		public float totalDamageDealt;
		public float overkillDamageTotal;
		public int killStreak;
		public int spellLoopCount;
		public int currentSpellInvocationId;

		// Status ailment tracking
		public int statusAilmentsApplied;
		public int statusAilmentsConsumed;

		// Damage by type (aggregate)
		public float physicalDamage;
		public float fireDamage;
		public float coldDamage;
		public float lightningDamage;
	}

	/// <summary>
	/// Per-spell telemetry metrics.
	/// Tracks detailed data for each spell for predicate evaluation.
	/// </summary>
	public struct SpellTelemetry
	{
		public int spellId;
		public int invocationCount;
		public int enemiesHit;
		public int enemiesKilled;
		public float totalDamageDealt;
		public float overkillDamage;
		
		// Damage by type for this spell
		public float physicalDamage;
		public float fireDamage;
		public float coldDamage;
		public float lightningDamage;
		
		// Status ailments applied by this spell
		public int statusAilmentsApplied;
		public StatusAilmentFlag statusAilmentsAppliedFlags;
	}

	/// <summary>
	/// Detailed hit information for tracking spell performance.
	/// </summary>
	public struct SpellHitData
	{
		public int spellId;
		public int spellInvocationId;
		public int enemyEntityId;
		public float damageDealt;
		public SpellDamageType damageType;
		public StatusAilmentFlag statusAilmentsApplied;
		public bool wasKill;
		public float overkillDamage;
	}

	/// <summary>
	/// Detailed kill information for tracking spell performance.
	/// </summary>
	public struct SpellKillData
	{
		public int spellId;
		public int spellInvocationId;
		public int enemyEntityId;
		public float damageDealt;
		public float overkillDamage;
		public EnemyCorruptionFlag enemyCorruption;
		public StatusAilmentFlag finalStatusAilments;
	}
}

