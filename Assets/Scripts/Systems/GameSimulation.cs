using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Configuration for initializing the game simulation. Passed to GameSimulation constructor.
/// </summary>
public class SimulationConfig
{
    public RectTransform SimulationZone;
    public float SpawnRate = 2f;
    public float GizmoRadius = 5f;
    public EnemySpawnTable SpawnTable;
}

/// <summary>
/// Runs the core game simulation: enemies, attack entities, collision, damage, and spell casting.
/// Owns all simulation systems and state. TestSceneManager (or another runner) drives Tick, rendering, and input.
/// </summary>
public class GameSimulation
{
    private readonly RectTransform _simulationZone;
    private readonly float _gizmoRadius;
    private readonly EnemySpawnTable _spawnTable;
    private EnemyRemovalBatch _enemyRemovals;
    private readonly List<Vector2> _spawnPositionsBuffer = new List<Vector2>();

    private EnemyManager _enemyManager;
    private EnemyMovementSystemLinear _movementSystem;
    private EnemyCullingSystem _cullingSystem;
    private EnemySpawner _spawner;
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

    private SimulationStepCommand[] _steps;
    private float _simulationTime;
    private float _frameDeltaTime;
    private readonly List<IDebugDrawable> _debugDrawables = new List<IDebugDrawable>();

    public GameSimulation(SimulationConfig config)
    {
        if (config?.SimulationZone == null)
            throw new ArgumentNullException(nameof(config), "SimulationZone is required.");

        _simulationZone = config.SimulationZone;
        _gizmoRadius = config.GizmoRadius;
        _spawnTable = config.SpawnTable;

        Rect r = _simulationZone.rect;

        _enemyManager = new EnemyManager(_simulationZone);
        _movementSystem = new EnemyMovementSystemLinear();
        _cullingSystem = new EnemyCullingSystem();
        _spawner = new EnemySpawner(config.SpawnRate, _simulationZone.rect.height);

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
        _enemyRemovals = new EnemyRemovalBatch(Allocator.Persistent);

        _debugDrawables.Add(_enemyManager.Grid);
        _debugDrawables.Add(new EnemyManagerGizmoDrawer(_enemyManager, _gizmoRadius));

        _steps = new[]
        {
            new SimulationStepCommand { Name = "Spawn", Execute = StepSpawnEnemies },
            new SimulationStepCommand { Name = "Move", Execute = StepMoveEnemies },
            new SimulationStepCommand { Name = "EnemyVisualTime", Execute = StepEnemyVisualTime },
            new SimulationStepCommand { Name = "AilmentTime", Execute = StepAilmentTime },
            new SimulationStepCommand { Name = "Cull", Execute = StepCull },
            new SimulationStepCommand { Name = "RemoveCulled", Execute = StepRemoveCulled },
            new SimulationStepCommand { Name = "BuildGrid", Execute = StepBuildGrid },
            new SimulationStepCommand { Name = "AttackTick", Execute = StepAttackTickAndMove },
            new SimulationStepCommand { Name = "Collision", Execute = StepCollision },
            new SimulationStepCommand { Name = "Damage", Execute = StepDamage },
            new SimulationStepCommand { Name = "AttackExpire", Execute = StepAttackExpire },
        };
    }

    /// <summary>Current simulation time in seconds.</summary>
    public float SimulationTime => _simulationTime;

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

    /// <summary>Rect of the simulation zone (for spawn/cull bounds).</summary>
    public Rect Rect => _simulationZone != null ? _simulationZone.rect : default;

    public NativeArray<Enemy> GetEnemies() => _enemyManager?.GetEnemies() ?? default;
    public NativeArray<AttackEntity> GetAttackEntities() => _attackEntityManager?.GetEntities() ?? default;
    public AttackEntityManager GetAttackEntityManager() => _attackEntityManager;
    public EnemyManager GetEnemyManager() => _enemyManager;

    /// <summary>Damage events from the last StepDamage. Call ClearDamageEvents after consuming (e.g. spawning damage numbers).</summary>
    public NativeArray<DamageEvent> GetDamageEvents() => _damageEvents.AsArray();

    /// <summary>Clears damage events after the caller has consumed them (e.g. spawned damage numbers).</summary>
    public void ClearDamageEvents() => _damageEvents.Clear();

    /// <summary>Status ailment applied events from the last StepDamage.</summary>
    public NativeArray<StatusAilmentAppliedEvent> GetStatusAilmentAppliedEvents() => _statusAilmentAppliedEvents.AsArray();

    /// <summary>Clears status ailment applied events after the caller has consumed them.</summary>
    public void ClearStatusAilmentAppliedEvents() => _statusAilmentAppliedEvents.Clear();

    /// <summary>
    /// Clears all enemies, attack entities, event buffers, and resets simulation time for a fresh round.
    /// Does not dispose NativeLists; just clears them.
    /// </summary>
    public void ResetForNewRound()
    {
        _enemyManager.Clear();
        _attackEntityManager.Clear();
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
        _enemyRemovals.Clear();
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
        _enemyRemovals.Dispose();
        _ailmentSystem.Dispose();
    }

    private void StepSpawnEnemies()
    {
        if (_spawnTable == null || _enemyManager == null)
            return;

        List<Vector2> origins = _spawner.GetSpawnEventOrigins(_simulationTime);
        if (origins.Count == 0) return;

        Rect r = Rect;
        uint seed = (uint)(_simulationTime * 1000f).GetHashCode();
        for (int i = 0; i < origins.Count; i++)
        {
            Vector2 worldOrigin = new Vector2(r.xMin, r.yMin + origins[i].y);
            var pick = _spawnTable.PickEnemyByWeight(seed + (uint)i);
            if (pick.enemy == null) continue;

            SpawnPattern pattern = pick.pattern != null ? pick.pattern : _spawnTable.fallbackSpawnPattern;
            if (pattern != null)
                pattern.GetPositions(worldOrigin, _spawnPositionsBuffer, seed + (uint)(i * 1000));
            else
            {
                _spawnPositionsBuffer.Clear();
                _spawnPositionsBuffer.Add(worldOrigin);
            }
            if (pick.positionScale != 1f && _spawnPositionsBuffer.Count > 0)
            {
                for (int j = 0; j < _spawnPositionsBuffer.Count; j++)
                {
                    Vector2 p = _spawnPositionsBuffer[j];
                    _spawnPositionsBuffer[j] = worldOrigin + (p - worldOrigin) * pick.positionScale;
                }
            }
            if (_spawnPositionsBuffer.Count > 0)
                _enemyManager.CreateEnemies(_spawnPositionsBuffer, pick.enemy);
        }
    }

    private void StepMoveEnemies()
    {
        _movementSystem.MoveEnemies(_enemyManager.GetEnemies(), _frameDeltaTime);
    }

    private void StepEnemyVisualTime()
    {
        EnemyVisualTimeSystem.Tick(_enemyManager.GetEnemies(), _frameDeltaTime);
    }

    private void StepAilmentTime()
    {
        _ailmentSystem.TickStatusAilmentDurations(_enemyManager.GetEnemies(), _frameDeltaTime);
    }

    private void StepBuildGrid()
    {
        _enemyManager.BuildGrid();
    }

    private void StepCull()
    {
        NativeArray<Enemy> enemies = _enemyManager.GetEnemies();
        JobHandle h = _cullingSystem.ScheduleCollectEnemiesPastRightEdge(
            enemies,
            Rect.xMax,
            _enemyRemovals.CulledPastBoundsIndices,
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
                _enemyManager.GetEnemies(),
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
            NativeArray<Enemy> enemies = _enemyManager.GetEnemies();
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
            _attackEntityManager.ValidateHitEvents(resolvedHitsRO, _enemyManager.EnemyCount);

            _chainSystem.ResolveChains(resolvedHitsRO, attackEntities, chainPolicies, _enemyManager.Grid, enemies.AsReadOnly());

            _damageSystem.ProcessHits(resolvedHitsRO, attackEntities, enemies, _hitEvents, _killEvents, _damageEvents);

            var appliers = new AttackEntityManagerAilmentAppliers { Manager = _attackEntityManager };
            _ailmentSystem.ProcessAilmentApplication(
                enemies,
                _damageEvents.AsArray(),
                appliers,
                _statusAilmentAppliedEvents,
                _simulationTime);

            _attackEntityManager.RecordRehitHits(resolvedHitsRO, attackEntities.AsReadOnly(), enemies.AsReadOnly());

            _enemyRemovals.HealthDepletedIndices.Clear();
            _enemyRemovals.HealthDepletedEntityIds.Clear();
            _deadEnemyRemovalSystem.CollectDeadEnemies(
                _enemyManager.GetEnemies(),
                _enemyRemovals.HealthDepletedIndices,
                _enemyRemovals.HealthDepletedEntityIds);
            ApplyDeadEnemyRemovals();
        }
    }

    private void ApplyCulledEnemyRemovals()
    {
        NativeList<int> indices = _enemyRemovals.CulledPastBoundsIndices;
        NativeList<int> entityIds = _enemyRemovals.CulledPastBoundsEntityIds;
        if (indices.Length == 0)
            return;
        _enemyManager.ApplyAscendingRemovalTrack(indices, entityIds);
        _ailmentSystem.ProcessEnemyRemovals(entityIds);
        indices.Clear();
        entityIds.Clear();
    }

    private void ApplyDeadEnemyRemovals()
    {
        NativeList<int> indices = _enemyRemovals.HealthDepletedIndices;
        NativeList<int> entityIds = _enemyRemovals.HealthDepletedEntityIds;
        if (indices.Length == 0)
            return;
        _enemyManager.ApplyAscendingRemovalTrack(indices, entityIds);
        _ailmentSystem.ProcessEnemyRemovals(entityIds);
        indices.Clear();
        entityIds.Clear();
    }

    private void StepAttackExpire()
    {
        if (_attackEntityManager.EntityCount == 0) return;

        _attackEntityManager.ValidateParallelLists();
        NativeArray<AttackEntity> entities = _attackEntityManager.GetEntities();
        _attackEntityCullingSystem.CollectRemovals(entities, Rect, _attackRemovalEvents);
        _pierceSystem.CollectRemovals(entities, _attackEntityManager.GetPiercePolicies(), _attackRemovalEvents);
        _expirationSystem.CollectRemovals(entities, _attackEntityManager.GetExpirationPolicies(), _attackRemovalEvents);
        _chainSystem.CollectRemovals(entities, _attackEntityManager.GetChainPolicies(), _attackRemovalEvents);
        _attackEntityManager.ApplyRemovals(_attackRemovalEvents);
        _attackRemovalEvents.Clear();
    }

    private struct AttackEntityManagerAilmentAppliers : AilmentSystem.AilmentApplierProvider
    {
        public AttackEntityManager Manager;

        public NativeArray<FrozenApplierRuntime> GetFrozenAppliers() => Manager.GetFrozenAppliers();
        public NativeArray<IgnitedApplierRuntime> GetIgnitedAppliers() => Manager.GetIgnitedAppliers();
        public NativeArray<ShockedApplierRuntime> GetShockedAppliers() => Manager.GetShockedAppliers();
        public NativeArray<PoisonedApplierRuntime> GetPoisonedAppliers() => Manager.GetPoisonedAppliers();
        public NativeArray<StunnedApplierRuntime> GetStunnedAppliers() => Manager.GetStunnedAppliers();
    }
}
