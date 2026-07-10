using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

[Serializable]
public class SimulationConfig
{
    [SerializeReference, SerializeInterface]
    [Tooltip("Spawn timing/origins. Pick Enemy Spawner from the type menu. If unset at runtime, a default EnemySpawner is created from Spawn Rate and the simulation zone height.")]
    public IEnemySpawner spawner;
    public RectTransform SimulationZone;
    public float SpawnRate = 2f;
    public float GizmoRadius = 5f;
}

/// <summary>
/// Runs the core game simulation: enemies, attack entities, collision, damage, and spell casting.
/// Owns all simulation systems and state. TestSceneManager (or another runner) drives Tick, rendering, and input.
/// Domain reads use <see cref="State"/>; subsystem handles use <see cref="EnemyManager"/> / <see cref="AttackEntityManager"/>.
/// </summary>
public partial class GameSimulation
{
    private readonly RectTransform _simulationZone;
    private readonly float _gizmoRadius;
    private EnemyRemovalBatch _enemyRemovals;

    private EnemyManager _enemyManager;
    private EnemyMovementSystemLinear _movementSystem;
    private EnemyCullingSystem _cullingSystem;
    private IEnemySpawner _spawner;
    private AttackEntityManager _attackEntityManager;
    private AttackEntityMovementSystem _attackMovementSystem;
    private AttackEntityTimeSystem _attackTimeSystem;
    private CollisionSystem _collisionSystem;
    private HitResolver _hitResolver;
    private ChainSystem _chainSystem;
    private DamageSystem _damageSystem;
    private DeadEnemyRemovalSystem _deadEnemyRemovalSystem;
    private PierceSystem _pierceSystem;
    private ExpirationSystem _expirationSystem;
    private AttackEntityCullingSystem _attackEntityCullingSystem;
    private AilmentSystem _ailmentSystem;

    private NativeList<CollisionEvent> _rawCollisionEvents;
    private NativeList<HitEvent> _resolvedHits;
    private NativeList<AttackEntityRemovalEvent> _attackRemovalEvents;
    private NativeList<EnemyHitEvent> _hitEvents;
    private NativeList<EnemyKilledEvent> _killEvents;
    private NativeList<DamageEvent> _damageEvents;
    private NativeList<StatusAilmentAppliedEvent> _statusAilmentAppliedEvents;
    private NativeList<TickDamageEvent> _tickDamageEvents;
    private NativeHashMap<int, float> _shockDamageMultiplierScratch;

    private SimulationStepCommand[] _steps;
    private float _simulationTime;
    private float _frameDeltaTime;
    private readonly List<IDebugDrawable> _debugDrawables = new List<IDebugDrawable>();

    /// <summary>Per-frame combat reaction contracts (managed; caller owns the list). Cleared at end of <see cref="StepCombatReactions"/>.</summary>
    private IReadOnlyList<CombatSpawnContract> _frameCombatContracts;

    private readonly SimulationState _state;

    /// <summary>LCD read of simulation-domain buffers and scalars.</summary>
    public SimulationState State => _state;

    public GameSimulation(SimulationConfig config)
    {
        if (config?.SimulationZone == null)
            throw new ArgumentNullException(nameof(config), "SimulationZone is required.");

        _simulationZone = config.SimulationZone;
        _gizmoRadius = config.GizmoRadius;

        Rect r = _simulationZone.rect;

        _enemyManager = new EnemyManager(_simulationZone);
        _movementSystem = new EnemyMovementSystemLinear();
        _cullingSystem = new EnemyCullingSystem();
        _spawner = config.spawner;

        _attackEntityManager = new AttackEntityManager();
        _attackMovementSystem = new AttackEntityMovementSystem();
        _attackTimeSystem = new AttackEntityTimeSystem();
        _collisionSystem = new CollisionSystem();
        _hitResolver = new HitResolver();
        _chainSystem = new ChainSystem();
        _damageSystem = new DamageSystem();
        _deadEnemyRemovalSystem = new DeadEnemyRemovalSystem();
        _pierceSystem = new PierceSystem();
        _expirationSystem = new ExpirationSystem();
        _attackEntityCullingSystem = new AttackEntityCullingSystem();
        _ailmentSystem = new AilmentSystem();

        _rawCollisionEvents = new NativeList<CollisionEvent>(64, Allocator.Persistent);
        _resolvedHits = new NativeList<HitEvent>(64, Allocator.Persistent);
        _attackRemovalEvents = new NativeList<AttackEntityRemovalEvent>(64, Allocator.Persistent);
        _hitEvents = new NativeList<EnemyHitEvent>(64, Allocator.Persistent);
        _killEvents = new NativeList<EnemyKilledEvent>(16, Allocator.Persistent);
        _damageEvents = new NativeList<DamageEvent>(64, Allocator.Persistent);
        _statusAilmentAppliedEvents = new NativeList<StatusAilmentAppliedEvent>(32, Allocator.Persistent);
        _tickDamageEvents = new NativeList<TickDamageEvent>(64, Allocator.Persistent);
        _shockDamageMultiplierScratch = new NativeHashMap<int, float>(256, Allocator.Persistent);
        _enemyRemovals = new EnemyRemovalBatch(Allocator.Persistent);

        _debugDrawables.Add(_enemyManager.Grid);
        _debugDrawables.Add(new EnemyManagerGizmoDrawer(_enemyManager, _gizmoRadius));

        _steps = new[]
        {
            new SimulationStepCommand { Name = "Spawn", Execute = StepSpawnEnemies },
            new SimulationStepCommand { Name = "Move", Execute = StepMoveEnemies },
            new SimulationStepCommand { Name = "EnemyVisualTime", Execute = StepEnemyVisualTime },
            new SimulationStepCommand { Name = "Cull", Execute = StepCull },
            new SimulationStepCommand { Name = "RemoveCulled", Execute = StepRemoveCulled },
            new SimulationStepCommand { Name = "BuildGrid", Execute = StepBuildGrid },
            new SimulationStepCommand { Name = "AttackTick", Execute = StepAttackTickAndMove },
            new SimulationStepCommand { Name = "Collision", Execute = StepCollision },
            new SimulationStepCommand { Name = "Damage", Execute = StepDamage },
            new SimulationStepCommand { Name = "AilmentTime", Execute = StepAilmentTime },
            new SimulationStepCommand { Name = "RemoveDeadEnemies", Execute = ProcessDeadFromHealthDepleted },
            new SimulationStepCommand { Name = "AttackExpire", Execute = StepAttackExpire },
            new SimulationStepCommand { Name = "CombatReactions", Execute = StepCombatReactions },
        };

        _state = new SimulationState(this);
    }

    private Rect PlayfieldRect => _simulationZone != null ? _simulationZone.rect : default;

    /// <summary>Number of simulation steps. Use with ExecuteStep and GetStepName.</summary>
    public int StepCount => _steps?.Length ?? 0;

    /// <summary>Name of the step at the given index.</summary>
    public string GetStepName(int index)
    {
        if (_steps == null || index < 0 || index >= _steps.Length) return string.Empty;
        return _steps[index].Name;
    }

    /// <summary>Advance simulation time by the given delta.</summary>
    public void AdvanceTime(float deltaTime)
    {
        _frameDeltaTime = deltaTime;
        _simulationTime += deltaTime;
    }

    /// <summary>Run the simulation step at the given index. Call in order each frame (or per debug phase).</summary>
    public void ExecuteStep(int stepIndex)
    {
        if (_steps == null || stepIndex < 0 || stepIndex >= _steps.Length) return;
        _steps[stepIndex].Execute();
    }

    /// <summary>Mutation/capability handle for enemy storage and grid (e.g. spell emission targeting).</summary>
    public EnemyManager EnemyManager => _enemyManager;

    /// <summary>Mutation/capability handle for attack entities (e.g. spell emission).</summary>
    public AttackEntityManager AttackEntityManager => _attackEntityManager;

    /// <summary>Active enemy spawn strategy (rate line, brush stroke, etc.).</summary>
    public IEnemySpawner Spawner => _spawner;

    /// <summary>Combat reaction contracts for the current frame; does not take ownership of the list.</summary>
    public void SetFrameCombatReactionContracts(IReadOnlyList<CombatSpawnContract> contracts)
    {
        _frameCombatContracts = contracts;
    }

    /// <summary>Clears the reference to frame combat reaction contracts (caller owns the list).</summary>
    public void ClearFrameCombatReactionContracts()
    {
        _frameCombatContracts = null;
    }

    /// <summary>Clears transient combat event lists after consumers have read them this frame.</summary>
    public void ClearFrameCombatEvents()
    {
        _killEvents.Clear();
        _damageEvents.Clear();
        _tickDamageEvents.Clear();
        _statusAilmentAppliedEvents.Clear();
    }

    /// <summary>
    /// Clears all enemies, attack entities, event buffers, and resets simulation time for a fresh round.
    /// Does not dispose NativeLists; just clears them.
    /// </summary>
    public void ResetForNewRound()
    {
        _enemyManager.Clear();
        _attackEntityManager.Clear();
        _ailmentSystem.Clear();
        _spawner.Reset();
        _simulationTime = 0f;
        _frameDeltaTime = 0f;

        _rawCollisionEvents.Clear();
        _resolvedHits.Clear();
        _attackRemovalEvents.Clear();
        _hitEvents.Clear();
        _killEvents.Clear();
        _damageEvents.Clear();
        _statusAilmentAppliedEvents.Clear();
        _tickDamageEvents.Clear();
        _enemyRemovals.Clear();
        ClearFrameCombatReactionContracts();
    }

    public IReadOnlyList<IDebugDrawable> GetDebugDrawables() => _debugDrawables;

    public void Dispose()
    {
        _enemyManager?.Dispose();
        _attackEntityManager?.Dispose();
        _collisionSystem?.Dispose();
        _chainSystem?.Dispose();
        if (_rawCollisionEvents.IsCreated) _rawCollisionEvents.Dispose();
        if (_resolvedHits.IsCreated) _resolvedHits.Dispose();
        if (_attackRemovalEvents.IsCreated) _attackRemovalEvents.Dispose();
        if (_hitEvents.IsCreated) _hitEvents.Dispose();
        if (_killEvents.IsCreated) _killEvents.Dispose();
        if (_damageEvents.IsCreated) _damageEvents.Dispose();
        if (_statusAilmentAppliedEvents.IsCreated) _statusAilmentAppliedEvents.Dispose();
        if (_tickDamageEvents.IsCreated) _tickDamageEvents.Dispose();
        if (_shockDamageMultiplierScratch.IsCreated) _shockDamageMultiplierScratch.Dispose();
        _enemyRemovals.Dispose();
        _ailmentSystem.Dispose();
    }

    private void StepSpawnEnemies()
    {
        if (_spawner == null || _enemyManager == null)
            return;

        List<EnemySpawnRequest> requests = _spawner.CollectSpawnRequests(_simulationTime, PlayfieldRect);
        for (int i = 0; i < requests.Count; i++)
        {
            EnemySpawnRequest req = requests[i];
            _enemyManager.CreateEnemies(req.positions, req.enemy);
        }
    }

    private void StepMoveEnemies()
    {
        EnemyBuffers enemies = _enemyManager.GetBuffers();
        if (enemies.AliveCount > 0)
            _movementSystem.MoveEnemies(enemies, _frameDeltaTime);
    }

    private void StepEnemyVisualTime()
    {
        EnemyVisualTimeSystem.Tick(_enemyManager.GetBuffers(), _frameDeltaTime);
    }

    private void StepAilmentTime()
    {
        EnemyBuffers enemies = _enemyManager.GetBuffers();
        if (enemies.AliveCount == 0 || _frameDeltaTime <= 0f)
        {
            _ailmentSystem.TickStatusAilmentDurations(enemies, _frameDeltaTime);
            return;
        }

        TickDamagePipeline.ProcessTimeBasedDotTicks(
            _ailmentSystem.IgniteStatus,
            _ailmentSystem.PoisonStatus,
            _ailmentSystem.BleedStatus,
            enemies,
            _frameDeltaTime,
            _simulationTime,
            _tickDamageEvents,
            _killEvents);

        ProcessDeadFromHealthDepleted();
        _ailmentSystem.TickStatusAilmentDurations(_enemyManager.GetBuffers(), _frameDeltaTime);
    }

    private void StepBuildGrid()
    {
        _enemyManager.BuildGrid();
    }

    private void StepCull()
    {
        EnemyBuffers enemies = _enemyManager.GetBuffers();
        JobHandle h = _cullingSystem.ScheduleCollectEnemiesPastRightEdge(
            enemies,
            PlayfieldRect.xMax,
            _enemyRemovals.CulledPastBoundsEntityIds);
        h.Complete();
    }

    private void StepRemoveCulled()
    {
        ApplyCulledEnemyRemovals();
    }

    private void StepAttackTickAndMove()
    {
        var attackEntities = _attackEntityManager.GetEntities();
        if (attackEntities.Length > 0)
        {
            _attackTimeSystem.Tick(attackEntities, _frameDeltaTime);
            _attackMovementSystem.MoveEntities(_attackEntityManager.GetEntities(), _frameDeltaTime);
        }
    }

    private void StepCollision()
    {
        if (_attackEntityManager.EntityCount > 0 && _enemyManager.EnemyCount > 0)
        {
            _enemyManager.ValidateGridForCurrentEnemies();
            _collisionSystem.Detect(
                _attackEntityManager.GetEntities(),
                _enemyManager.GetBuffers(),
                _enemyManager.Grid,
                _rawCollisionEvents);
        }
        else
        {
            _rawCollisionEvents.Clear();
        }
    }

    private void StepDamage()
    {
        _hitEvents.Clear();
        _killEvents.Clear();
        _statusAilmentAppliedEvents.Clear();

        if (_rawCollisionEvents.Length > 0)
        {
            NativeArray<AttackEntity> attackEntities = _attackEntityManager.GetEntities();
            EnemyBuffers enemies = _enemyManager.GetBuffers();
            NativeArray<ChainPolicyRuntime> chainPolicies = _attackEntityManager.GetChainPolicies();

            // ── Combat pipeline ──
            //   _rawCollisionEvents
            //       → [HitResolver]  → _resolvedHits              (mutates rehitPolicies)
            //       → [ChainSystem]                                (mutates attackEntities, chainPolicies)
            //       → [DamageSystem] → _hitEvents, _killEvents,    (mutates attackEntities, enemies)
            //                          _damageEvents
            //       → [AilmentSystem]                              (parallel track jobs → NativeLists, then apply slice + events)
            //       → [RecordRehit]                                (mutates internal rehit records)

            _attackEntityManager.ValidateParallelLists();

            _hitResolver.Resolve(
                _rawCollisionEvents.AsArray().AsReadOnly(),
                attackEntities.AsReadOnly(),
                _attackEntityManager.GetPiercePolicies().AsReadOnly(),
                _attackEntityManager.GetRehitPolicies(),
                _resolvedHits);

            NativeArray<HitEvent>.ReadOnly resolvedHitsRO = _resolvedHits.AsArray().AsReadOnly();
            _attackEntityManager.ValidateHitEvents(resolvedHitsRO, _enemyManager.SlotCount);

            _chainSystem.ResolveChains(resolvedHitsRO, attackEntities, chainPolicies, _enemyManager.Grid, enemies.Motion.AsReadOnly());

            _ailmentSystem.BuildShockDamageMultipliers(_shockDamageMultiplierScratch);
            _damageSystem.ProcessHits(
                resolvedHitsRO,
                attackEntities,
                enemies,
                _hitEvents,
                _killEvents,
                _damageEvents,
                _shockDamageMultiplierScratch,
                _attackEntityManager.HitModifierSets);

            var appliers = new AttackEntityManagerAilmentAppliers { Manager = _attackEntityManager };
            _ailmentSystem.ProcessAilmentApplication(
                enemies,
                _damageEvents.AsArray(),
                appliers,
                _statusAilmentAppliedEvents,
                _simulationTime);

            _attackEntityManager.RecordRehitHits(resolvedHitsRO, attackEntities.AsReadOnly());
        }
    }

    private void ProcessDeadFromHealthDepleted()
    {
        _enemyRemovals.HealthDepletedEntityIds.Clear();
        _deadEnemyRemovalSystem.CollectDeadEnemies(
            _enemyManager.GetBuffers(),
            _enemyRemovals.HealthDepletedEntityIds);
        ApplyDeadEnemyRemovals();
    }

    private void ApplyCulledEnemyRemovals()
    {
        NativeList<EntityId> entityIds = _enemyRemovals.CulledPastBoundsEntityIds;
        if (entityIds.Length == 0)
            return;
        _enemyManager.ApplyRemovals(entityIds);
        _ailmentSystem.ProcessEnemyRemovals(entityIds);
        entityIds.Clear();
    }

    private void ApplyDeadEnemyRemovals()
    {
        NativeList<EntityId> entityIds = _enemyRemovals.HealthDepletedEntityIds;
        if (entityIds.Length == 0)
            return;
        _enemyManager.ApplyRemovals(entityIds);
        _ailmentSystem.ProcessEnemyRemovals(entityIds);
        entityIds.Clear();
    }

    private void StepAttackExpire()
    {
        if (_attackEntityManager.EntityCount == 0) return;

        _attackEntityManager.ValidateParallelLists();
        NativeArray<AttackEntity> entities = _attackEntityManager.GetEntities();
        _attackEntityCullingSystem.CollectRemovals(entities, PlayfieldRect, _attackRemovalEvents);
        _pierceSystem.CollectRemovals(entities, _attackEntityManager.GetPiercePolicies(), _attackRemovalEvents);
        _expirationSystem.CollectRemovals(entities, _attackEntityManager.GetExpirationPolicies(), _attackRemovalEvents);
        _chainSystem.CollectRemovals(entities, _attackEntityManager.GetChainPolicies(), _attackRemovalEvents);
        _attackEntityManager.ApplyRemovals(_attackRemovalEvents);
        _attackRemovalEvents.Clear();
    }

    private void StepCombatReactions()
    {
        if (_frameCombatContracts == null || _frameCombatContracts.Count == 0)
            return;

        CombatReactionProcessor.ProcessFrameCombatReactions(
            _killEvents.AsArray(),
            _statusAilmentAppliedEvents.AsArray(),
            _frameCombatContracts,
            _attackEntityManager);

        _frameCombatContracts = null;
    }

    private struct AttackEntityManagerAilmentAppliers : AilmentSystem.AilmentApplierProvider
    {
        public AttackEntityManager Manager;

        public NativeArray<FrozenApplierRuntime> GetFrozenAppliers() => Manager.GetFrozenAppliers();
        public NativeArray<IgnitedApplierRuntime> GetIgnitedAppliers() => Manager.GetIgnitedAppliers();
        public NativeArray<ShockedApplierRuntime> GetShockedAppliers() => Manager.GetShockedAppliers();
        public NativeArray<PoisonedApplierRuntime> GetPoisonedAppliers() => Manager.GetPoisonedAppliers();
        public NativeArray<StunnedApplierRuntime> GetStunnedAppliers() => Manager.GetStunnedAppliers();
        public NativeArray<BleedApplierRuntime> GetBleedAppliers() => Manager.GetBleedAppliers();
    }
}
