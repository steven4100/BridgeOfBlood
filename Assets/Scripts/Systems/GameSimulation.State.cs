using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using UnityEngine;

public partial class GameSimulation
{
    /// <summary>
    /// Lowest-common-denominator read of simulation-domain state: playfield, entity buffers, and combat/ailment events.
    /// Contains no rendering or presentation types. NativeArrays are views into persistent buffers owned by
    /// <see cref="GameSimulation"/>; they remain valid until the next simulation mutation or until frame combat events are cleared.
    /// </summary>
    public sealed class SimulationState
    {
        private readonly GameSimulation _owner;

        internal SimulationState(GameSimulation owner)
        {
            _owner = owner;
        }

        /// <summary>Simulation clock in seconds.</summary>
        public float SimulationTime => _owner._simulationTime;

        /// <summary>
        /// Playfield in simulation space: x = 0 (left) .. width (right); y = 0 at vertical center (±height/2).
        /// </summary>
        public Rect Playfield => _owner._playfield;

        /// <summary>Parallel enemy column views; valid until next enemy list mutation.</summary>
        public EnemyBuffers EnemyBuffers => _owner._enemyManager.GetBuffers();

        /// <summary>Live attack entities (identity, transform, visuals). Combat stats live on parallel lists.</summary>
        public NativeArray<AttackEntity> AttackEntities => _owner._attackEntityManager.GetEntities();

        public NativeArray<HitBoxRuntime> AttackHitBoxes => _owner._attackEntityManager.GetHitBoxes();

        /// <summary>Damage events produced by the last StepDamage.</summary>
        public NativeArray<DamageEvent> DamageEvents => _owner._damageEvents.AsArray();

        /// <summary>DoT / tick damage events produced during AilmentTime.</summary>
        public NativeArray<TickDamageEvent> TickDamageEvents => _owner._tickDamageEvents.AsArray();

        /// <summary>Enemy kills from hit damage (Damage step) and from DoT (AilmentTime).</summary>
        public NativeArray<EnemyKilledEvent> KillEvents => _owner._killEvents.AsArray();

        /// <summary>Status ailments applied during StepDamage.</summary>
        public NativeArray<StatusAilmentAppliedEvent> StatusAilmentAppliedEvents =>
            _owner._statusAilmentAppliedEvents.AsArray();

        public int EnemyCount => _owner._enemyManager.EnemyCount;

        public int EnemiesSpawnedThisRound => _owner._enemyManager.SpawnedThisRound;

        public int AttackEntityCount => _owner._attackEntityManager.EntityCount;
    }
}
