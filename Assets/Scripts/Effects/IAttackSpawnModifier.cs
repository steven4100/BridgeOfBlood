using BridgeOfBlood.Data.Enemies;

/// <summary>Optional hook to tweak a combat-reaction spawn after <see cref="AttackEntityBuilder.Build"/>. For ailments, <paramref name="combatSnapshot"/> is resolved from live enemy buffers in <see cref="CombatReactionProcessor"/> (default if the enemy row was removed).</summary>
public interface IAttackSpawnModifier
{
	void ModifyKillSpawn(in EnemyKilledEvent evt, ref AttackEntitySpawnPayload payload);
	void ModifyAilmentSpawn(in StatusAilmentAppliedEvent evt, in EnemyCombatSnapshot combatSnapshot, ref AttackEntitySpawnPayload payload);
}
