using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

[Serializable]
public class SimulationConfig
{
    public float SpawnRate = 2f;
    public float GizmoRadius = 5f;
}

/// <summary>
/// Runs the core game simulation: enemies, attack entities, collision, damage, and spell casting.
/// Owns all simulation systems and state. GameManager (or another runner) drives Tick, rendering, and input.
/// Domain reads use <see cref="State"/>; subsystem handles use <see cref="EnemyManager"/> / <see cref="AttackEntityManager"/>.
/// </summary>
public partial class GameSimulation
{
    private Rect _playfield;
    private readonly float _gizmoRadius;
    private EnemyRemovalBatch _enemyRemovals;

    private EnemyManager _enemyManager;
    private EnemyMovementSystemLinear _movementSystem;
    private EnemyCullingSystem _cullingSystem;
    private IEnemySpawner _spawner;
    private float _enemyHealthMultiplier = 1f;
    private float _enemyMoveSpeedMultiplier = 1f;
    private AttackEntityManager _attackEntityManager;
    private AttackEntityMovementSystem _attackMovementSystem;
    private AttackEntityTimeSystem _attackTimeSystem;
    private CollisionSystem _collisionSystem;
    private HitResolver _hitResolver;
    private ChainSystem _chainSystem;
    private HitModifierSystem _hitModifierSystem;
    private DamageSystem _damageSystem;
    private KnockbackSystem _knockbackSystem;
    private DeadEnemyRemovalSystem _deadEnemyRemovalSystem;
    private PierceSystem _pierceSystem;
    private ExpirationSystem _expirationSystem;
    private AttackEntityCullingSystem _attackEntityCullingSystem;
    private AilmentSystem _ailmentSystem;
    private readonly MapObjectRegistry _mapObjects = new MapObjectRegistry();

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

    /// <summary>Frame modification catalog (caller owns). Used by hit modifiers; may be null.</summary>
    private ModificationCatalog _frameModifications;

    private readonly SimulationState _state;

    /// <summary>LCD read of simulation-domain buffers and scalars.</summary>
    public SimulationState State => _state;

    public GameSimulation(SimulationConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _gizmoRadius = config.GizmoRadius;

        _enemyManager = new EnemyManager(default);
        _movementSystem = new EnemyMovementSystemLinear();
        _cullingSystem = new EnemyCullingSystem();

        _hitModifierSystem = new HitModifierSystem();
        _attackEntityManager = new AttackEntityManager();
        _attackMovementSystem = new AttackEntityMovementSystem();
        _attackTimeSystem = new AttackEntityTimeSystem();
        _collisionSystem = new CollisionSystem();
        _hitResolver = new HitResolver();
        _chainSystem = new ChainSystem();
        _damageSystem = new DamageSystem();
        _knockbackSystem = new KnockbackSystem();
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

        _debugDrawables.Add(new EnemyGridGizmoDrawer(_enemyManager));
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

    private Rect PlayfieldRect => _playfield;

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

    /// <summary>Live map objects placed by map modifiers. Cleared with each round reset.</summary>
    public MapObjectRegistry MapObjects => _mapObjects;

    /// <summary>Active enemy spawn strategy (rate line, brush stroke, etc.).</summary>
    public IEnemySpawner Spawner => _spawner;

    /// <summary>
    /// Assigns the session playfield/spawner/speed and this round's health multiplier.
    /// Call before <see cref="ResetForNewRound"/>.
    /// </summary>
    public void ApplyRoundConfig(GameConfig gameConfig, int roundNumber)
    {
        RoundConfig round = gameConfig.GetRoundConfig(roundNumber);
        _playfield = gameConfig.ResolvePlayfield();
        _enemyManager.RebuildSpatialGrid(_playfield);
        _spawner = gameConfig.spawner;
        _enemyMoveSpeedMultiplier = gameConfig.enemyMoveSpeedMultiplier;
        _enemyHealthMultiplier = round.enemyHealthMultiplier;
    }

    /// <summary>Frame modification catalog for hit modifiers; does not take ownership.</summary>
    public void SetFrameModifications(ModificationCatalog catalog)
    {
        _frameModifications = catalog;
    }

    /// <summary>Clears the reference to the frame modification catalog (caller owns it).</summary>
    public void ClearFrameModifications()
    {
        _frameModifications = null;
    }

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
        _attackRemovalEvents.Clear();
    }

    /// <summary>
    /// Immediately drains the active spawner into the enemy manager, then rebuilds the spatial grid.
    /// The map-modifier scene uses this after reset with <see cref="NoiseSheetEnemySpawner"/>, whose
    /// first collection emits the entire sheet and marks the one-shot spawner as consumed.
    /// </summary>
    public void FlushSpawnerNow()
    {
        CollectAndCreateEnemies();
        _enemyManager.BuildGrid();
    }

    /// <summary>
    /// Clears all enemies, attack entities, event buffers, and resets simulation time for a fresh round.
    /// Does not dispose NativeLists; just clears them.
    /// </summary>
    public void ResetForNewRound()
    {
        _enemyManager.Clear();
        _mapObjects.Clear();
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
        _mapObjects.Dispose();
    }

    private void StepSpawnEnemies()
    {
        CollectAndCreateEnemies();
    }

    void CollectAndCreateEnemies()
    {
        List<EnemySpawnRequest> requests =
            _spawner.CollectSpawnRequests(_simulationTime, PlayfieldRect);
        for (int i = 0; i < requests.Count; i++)
        {
            EnemySpawnRequest req = requests[i];
            _enemyManager.CreateEnemies(
                req.positions,
                req.enemy,
                _enemyHealthMultiplier,
                _enemyMoveSpeedMultiplier);
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
        if (_attackEntityManager.EntityCount == 0)
            return;

        var attackEntities = _attackEntityManager.GetEntities();
        var alive = _attackEntityManager.GetAlive();
        _attackTimeSystem.Tick(attackEntities, _attackEntityManager.GetHitBoxes(), alive, _frameDeltaTime);
        _attackMovementSystem.MoveEntities(
            attackEntities,
            alive,
            _attackEntityManager.GetMotionPolicies(),
            _frameDeltaTime);
    }

    private void StepCollision()
    {
        if (_attackEntityManager.EntityCount > 0 && _enemyManager.EnemyCount > 0)
        {
            _enemyManager.ValidateGridForCurrentEnemies();
            _collisionSystem.Detect(
                _attackEntityManager.GetEntities(),
                _attackEntityManager.GetAlive(),
                _attackEntityManager.GetHitBoxes(),
                _enemyManager.GetBuffers(),
                _enemyManager.Grid,
                _enemyManager.ComputeMaxHurtboxRadius(),
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
            //       → [HitResolver]  → _resolvedHits              (mutates rehitPolicies; copies attack base stats onto hits)
            //       → [ChainSystem]                                (mutates attackEntities, chainPolicies)
            //       → [HitModifierSystem]                          (edits hit damage/crit in place)
            //       → [MapObjectBehaviors]                         (reads hits, may start spell casts)
            //       → [MapObjectHitModifiers]                      (pass-through bonuses on attack ids)
            //       → [DamageSystem] → _hitEvents, _killEvents,    (mutates attackEntities, enemies)
            //                          _damageEvents
            //       → [KnockbackSystem]                            (mutates enemy motion from damage events)
            //       → [AilmentSystem]                              (parallel track jobs → NativeLists, then apply slice + events)
            //       → [RecordRehit]                                (mutates internal rehit records)

            _attackEntityManager.ValidateParallelLists();

            _hitResolver.Resolve(
                _rawCollisionEvents.AsArray().AsReadOnly(),
                attackEntities.AsReadOnly(),
                _attackEntityManager.GetPhysicalDamages().AsReadOnly(),
                _attackEntityManager.GetColdDamages().AsReadOnly(),
                _attackEntityManager.GetFireDamages().AsReadOnly(),
                _attackEntityManager.GetLightningDamages().AsReadOnly(),
                _attackEntityManager.GetCrits().AsReadOnly(),
                _attackEntityManager.GetPiercePolicies().AsReadOnly(),
                _attackEntityManager.GetRehitPolicies(),
                _resolvedHits);

            NativeArray<HitEvent>.ReadOnly resolvedHitsRO = _resolvedHits.AsArray().AsReadOnly();
            _attackEntityManager.ValidateHitEvents(resolvedHitsRO, _enemyManager.SlotCount);

            _chainSystem.ResolveChains(
                resolvedHitsRO,
                attackEntities,
                chainPolicies,
                _attackEntityManager.GetMotionPolicies(),
                _enemyManager.Grid,
                enemies.Motion.AsReadOnly());

            _hitModifierSystem.Apply(_frameModifications, _resolvedHits, enemies);

            _mapObjects.ProcessHits(_resolvedHits, enemies, _attackEntityManager, _enemyManager, _simulationTime);
            _mapObjects.ApplyHitModifiers(_resolvedHits, enemies);

            _ailmentSystem.BuildShockDamageMultipliers(_shockDamageMultiplierScratch);
            _damageSystem.ProcessHits(
                _resolvedHits.AsArray().AsReadOnly(),
                attackEntities,
                enemies,
                _hitEvents,
                _killEvents,
                _damageEvents,
                _shockDamageMultiplierScratch);

            _knockbackSystem.Apply(
                _damageEvents.AsArray().AsReadOnly(),
                attackEntities.AsReadOnly(),
                _attackEntityManager.GetKnockbacks().AsReadOnly(),
                enemies);

            var appliers = new AttackEntityManagerAilmentAppliers { Manager = _attackEntityManager };
            _ailmentSystem.ProcessAilmentApplication(
                enemies,
                _damageEvents.AsArray(),
                appliers,
                _statusAilmentAppliedEvents,
                _simulationTime);

            _attackEntityManager.RecordRehitHits(resolvedHitsRO, attackEntities.AsReadOnly());
        }

        _mapObjects.Tick(_frameDeltaTime, _simulationTime, _attackEntityManager, _enemyManager, _enemyManager.GetBuffers());
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
        _attackRemovalEvents.Clear();
        if (_attackEntityManager.EntityCount == 0) return;

        _attackEntityManager.ValidateParallelLists();
        NativeArray<AttackEntity> entities = _attackEntityManager.GetEntities();
        NativeArray<byte> alive = _attackEntityManager.GetAlive();
        _attackEntityCullingSystem.CollectRemovals(entities, alive, PlayfieldRect, _attackRemovalEvents);
        _pierceSystem.CollectRemovals(entities, alive, _attackEntityManager.GetPiercePolicies(), _attackRemovalEvents);
        _expirationSystem.CollectRemovals(entities, alive, _attackEntityManager.GetExpirationPolicies(), _attackRemovalEvents);
        _chainSystem.CollectRemovals(entities, alive, _attackEntityManager.GetChainPolicies(), _attackRemovalEvents);
        _attackEntityManager.ApplyRemovals(_attackRemovalEvents);
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

    private struct EnemyGridGizmoDrawer : IDebugDrawable
    {
        public EnemyManager Manager;

        public EnemyGridGizmoDrawer(EnemyManager manager)
        {
            Manager = manager;
        }

        public void DrawGizmos(Transform transform)
        {
            Manager.Grid.DrawGizmos(transform);
        }
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
