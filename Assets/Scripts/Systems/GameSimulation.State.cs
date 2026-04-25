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

        /// <summary>Playfield rectangle in simulation space (spawn, cull, spatial queries).</summary>
        public Rect Playfield => _owner._simulationZone != null ? _owner._simulationZone.rect : default;

        /// <summary>Parallel enemy column views; valid until next enemy list mutation.</summary>
        public EnemyBuffers EnemyBuffers => _owner._enemyManager.GetBuffers();

        /// <summary>Live attack entities.</summary>
        public NativeArray<AttackEntity> AttackEntities => _owner._attackEntityManager.GetEntities();

        /// <summary>Entity id → index into enemy columns; rebuilt at the start of each AilmentTime step.</summary>
        public NativeHashMap<int, int> EnemyEntityIdToIndex => _owner._enemyEntityIdToIndex;

        /// <summary>Damage events produced by the last StepDamage.</summary>
        public NativeArray<DamageEvent> DamageEvents => _owner._damageEvents.AsArray();

        /// <summary>DoT / tick damage events produced during AilmentTime.</summary>
        public NativeArray<TickDamageEvent> TickDamageEvents => _owner._tickDamageEvents.AsArray();

        /// <summary>Status ailments applied during StepDamage.</summary>
        public NativeArray<StatusAilmentAppliedEvent> StatusAilmentAppliedEvents =>
            _owner._statusAilmentAppliedEvents.AsArray();

        public int EnemyCount => _owner._enemyManager.EnemyCount;

        public int AttackEntityCount => _owner._attackEntityManager.EntityCount;
    }
}
