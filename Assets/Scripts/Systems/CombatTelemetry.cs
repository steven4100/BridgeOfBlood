namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Core combat metrics reusable at every aggregation level (frame, spell cast, spell loop, round, game).
	/// Value type -- accumulate via <see cref="Accumulate"/> and reset via <see cref="Reset"/>.
	/// </summary>
	public struct CombatMetrics
	{
		public int hits;
		public int kills;
		public int crits;
		public float totalDamage;
		public float physicalDamage;
		public float fireDamage;
		public float coldDamage;
		public float lightningDamage;
		public float overkillDamage;
		public float bloodExtracted;
		public int attackEntitiesExpired;
		public int frozenApplied;
		public int ignitedApplied;
		public int shockedApplied;
		public int poisonedApplied;
		public int stunnedApplied;
		public int bleedingApplied;
		public float duration;

		public int TotalAilmentsApplied => frozenApplied + ignitedApplied + shockedApplied + poisonedApplied + stunnedApplied + bleedingApplied;
		public float DPS => duration > 0f ? totalDamage / duration : 0f;

		public void Accumulate(in CombatMetrics other)
		{
			hits += other.hits;
			kills += other.kills;
			crits += other.crits;
			totalDamage += other.totalDamage;
			physicalDamage += other.physicalDamage;
			fireDamage += other.fireDamage;
			coldDamage += other.coldDamage;
			lightningDamage += other.lightningDamage;
			overkillDamage += other.overkillDamage;
			bloodExtracted += other.bloodExtracted;
			attackEntitiesExpired += other.attackEntitiesExpired;
			frozenApplied += other.frozenApplied;
			ignitedApplied += other.ignitedApplied;
			shockedApplied += other.shockedApplied;
			poisonedApplied += other.poisonedApplied;
			stunnedApplied += other.stunnedApplied;
			bleedingApplied += other.bleedingApplied;
			duration += other.duration;
		}

		public void Reset()
		{
			this = default;
		}
	}

	/// <summary>
	/// Per-spell combat metrics for a given time window.
	/// </summary>
	public struct SpellCombatMetrics
	{
		public int spellId;
		public int invocationCount;
		public CombatMetrics metrics;
	}

	/// <summary>
	/// Snapshot of combat telemetry for a single simulation frame.
	/// </summary>
	public struct FrameSnapshot
	{
		public CombatMetrics aggregate;
		public float deltaTime;
		public float simulationTime;
	}

	/// <summary>
	/// Snapshot of combat telemetry accumulated during a single spell's active window
	/// (from when this spell is cast until the next spell is cast).
	/// </summary>
	public struct SpellCastSnapshot
	{
		public CombatMetrics aggregate;
		public int spellId;
		public int invocationId;
	}

	/// <summary>
	/// Snapshot of combat telemetry accumulated across one full spell loop (all N spells cast once).
	/// </summary>
	public struct SpellLoopSnapshot
	{
		public CombatMetrics aggregate;
		public int loopIndex;
		public SpellCombatMetrics[] perSpell;
	}

	/// <summary>
	/// Snapshot of combat telemetry accumulated across an entire round.
	/// </summary>
	public struct RoundSnapshot
	{
		public CombatMetrics aggregate;
		public int roundNumber;
		public int loopsCompleted;
		public SpellCombatMetrics[] perSpell;
	}

	/// <summary>
	/// Snapshot of combat telemetry accumulated across the entire game.
	/// </summary>
	public struct GameSnapshot
	{
		public CombatMetrics aggregate;
		public int roundsCompleted;
		public SpellCombatMetrics[] perSpell;
	}
}
