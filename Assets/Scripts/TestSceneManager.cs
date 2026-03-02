using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using System;

/// <summary>
/// A named simulation step that can be executed, timed, and stepped through by the debug controller.
/// </summary>
public struct SimulationStepCommand
{
    public string Name;
    public Action Execute;
}

public class TestSceneManager : MonoBehaviour
{
    [Header("Simulation")]
    [Tooltip("Enemies spawned per second along the left edge")]
    public float spawnRate = 2f;

    [Tooltip("Rect in which enemies spawn on the left and move right")]
    public RectTransform simulationZone;

    Rect rect => simulationZone.rect;

    [Tooltip("Enemy stats and behavior for spawned enemies")]
    public EnemyAuthoringData enemyAuthoringData;

    [Header("Rendering")]
    [Tooltip("Camera that renders the game (used to place instanced meshes on screen)")]
    public Camera renderCamera;

    public Material material;
    public Material attackMaterial;

    [Tooltip("Material for damage number rendering. If null, uses default with generated digit atlas.")]
    public Material damageNumberMaterial;

    [Tooltip("Size of each enemy quad in rect-local units")]
    public float enemyScale = 10f;

    [Header("Player")]
    [Tooltip("Player movement speed in rect-local units per second")]
    public float playerMoveSpeed = 100f;

    [Tooltip("Optional. Assign a PlayerRenderer (e.g. on a child of the simulation zone) to visualize the player.")]
    public PlayerRenderer playerRenderer;

    [Header("Spells")]
    [Tooltip("Ordered list of spells in the loop. Cast timing uses each spell's castCompletionDuration.")]
    public List<SpellAuthoringData> spells = new List<SpellAuthoringData>();

    [Tooltip("Key that requests casting the next spell in the loop. Only casts when this is pressed and enough time has elapsed.")]
    public KeyCode castInputKey = KeyCode.Space;

    [Header("Debug")]
    [Tooltip("Gizmo sphere radius at each unit position (Scene view, Play mode only)")]
    public float gizmoRadius = 5f;

    [Tooltip("When enabled, logs timing of main Update steps to the console every frame.")]
    public bool debugLogTiming;

    [Tooltip("Optional debug controller for frame/phase stepping. If null, simulation runs normally.")]
    public SimulationDebugController debugController;

    private Player _player;
    private EnemyManager _enemyManager;
    private EnemyMovementSystemLinear _movementSystem;
    private EnemyCullingSystem _cullingSystem;
    private EnemySpawner _spawner;
    private EnemyRenderSystem _renderSystem;
    private AttackEntityManager _attackEntityManager;
    private AttackEntityMovementSystem _attackMovementSystem;
    private AttackEntityTimeSystem _attackTimeSystem;
    private AttackEntityDebugRenderer _attackDebugRenderer;
    private SpellEmissionHandler _emissionHandler;
    private LoopedSpellCaster _loopedSpellCaster;
    private CollisionSystem _collisionSystem;
    private HitResolver _hitResolver;
    private ChainSystem _chainSystem;
    private DamageSystem _damageSystem;
    private DeadEnemyRemovalSystem _deadEnemyRemovalSystem;
    private PierceSystem _pierceSystem;
    private ExpirationSystem _expirationSystem;
    private DamageNumberManager _damageNumberManager;
    private DamageNumberRenderSystem _damageNumberRenderSystem;
    private NativeList<CollisionEvent> _rawCollisionEvents;
    private NativeList<HitEvent> _resolvedHits;
    private NativeList<AttackEntityRemovalEvent> _attackRemovalEvents;
    private NativeList<EnemyHitEvent> _hitEvents;
    private NativeList<EnemyKilledEvent> _killEvents;
    private readonly List<IDebugDrawable> _debugDrawables = new List<IDebugDrawable>();
    private SimulationStepCommand[] _steps;
    private float _simulationTime;
    private float _frameDeltaTime;
    private readonly List<int> _toRemove = new List<int>();

    void Start()
    {
        _renderSystem = new EnemyRenderSystem(material: material, instanceScale: enemyScale);
        _enemyManager = new EnemyManager(simulationZone);
        _movementSystem = new EnemyMovementSystemLinear();
        _cullingSystem = new EnemyCullingSystem();
        _spawner = new EnemySpawner(spawnRate, simulationZone.rect.height);

        Rect r = rect;
        _player = new Player(
            new Unity.Mathematics.float2(r.center.x, r.center.y),
            playerMoveSpeed);

        if (playerRenderer != null)
            playerRenderer.Player = _player;

        _attackEntityManager = new AttackEntityManager();
        _attackMovementSystem = new AttackEntityMovementSystem();
        _attackTimeSystem = new AttackEntityTimeSystem();
        _attackDebugRenderer = new AttackEntityDebugRenderer(_attackEntityManager, attackMaterial);
        _collisionSystem = new CollisionSystem();
        _hitResolver = new HitResolver();
        _chainSystem = new ChainSystem();
        _damageSystem = new DamageSystem();
        _deadEnemyRemovalSystem = new DeadEnemyRemovalSystem();
        _rawCollisionEvents = new NativeList<CollisionEvent>(64, Allocator.Persistent);
        _resolvedHits = new NativeList<HitEvent>(64, Allocator.Persistent);
        _attackRemovalEvents = new NativeList<AttackEntityRemovalEvent>(64, Allocator.Persistent);
        _hitEvents = new NativeList<EnemyHitEvent>(64, Allocator.Persistent);
        _killEvents = new NativeList<EnemyKilledEvent>(16, Allocator.Persistent);
        _pierceSystem = new PierceSystem();
        _expirationSystem = new ExpirationSystem();
        _damageNumberManager = new DamageNumberManager();
        _damageNumberRenderSystem = new DamageNumberRenderSystem(damageNumberMaterial);

        _emissionHandler = new SpellEmissionHandler(_attackEntityManager);
        SpellInvoker loopInvoker = new SpellInvoker(_emissionHandler);
        List<Spell> runtimeSpells = BuildRuntimeSpells(spells);
        _loopedSpellCaster = new LoopedSpellCaster(runtimeSpells, loopInvoker);

        _debugDrawables.Add(_enemyManager.Grid);
        _debugDrawables.Add(new EnemyManagerGizmoDrawer(_enemyManager, gizmoRadius));
        _debugDrawables.Add(_attackDebugRenderer);

        _steps = new[]
        {
            new SimulationStepCommand { Name = "Spawn",        Execute = StepSpawnEnemies },
            new SimulationStepCommand { Name = "Move",         Execute = StepMoveEnemies },
            new SimulationStepCommand { Name = "BuildGrid",    Execute = StepBuildGrid },
            new SimulationStepCommand { Name = "Cull",         Execute = StepCull },
            new SimulationStepCommand { Name = "RemoveCulled", Execute = StepRemoveCulled },
            new SimulationStepCommand { Name = "AttackTick",   Execute = StepAttackTick },
            new SimulationStepCommand { Name = "Collision",    Execute = StepCollision },
            new SimulationStepCommand { Name = "Damage",       Execute = StepDamage },
            new SimulationStepCommand { Name = "AttackExpire", Execute = StepAttackExpire },
        };

        if (debugController != null)
            debugController.Initialize(_steps.Length);
    }

    void Update()
    {
        if (simulationZone == null) return;

        bool hasController = debugController != null;
        if (hasController)
            debugController.ProcessInput();

        _player.Update(Time.deltaTime, rect);

        bool castRequested = Input.GetKeyDown(castInputKey);
        _loopedSpellCaster?.AttemptToCastNextSpell(_simulationTime, _player.Position, spells, castRequested);
        _loopedSpellCaster?.Update(_simulationTime, -1 * GetCastForward());
        _emissionHandler?.Update(_simulationTime);

        bool advanceTime = !hasController || debugController.ShouldAdvanceTime;
        if (advanceTime)
        {
            _frameDeltaTime = hasController ? debugController.DeltaTime : Time.deltaTime;
            _simulationTime += _frameDeltaTime;
        }

        Stopwatch sw = debugLogTiming ? new Stopwatch() : null;
        long totalMs = 0;

        for (int i = 0; i < _steps.Length; i++)
        {
            if (!hasController || debugController.ShouldRunPhase(i, _steps[i].Name))
            {
                sw?.Restart();
                _steps[i].Execute();
                if (sw != null)
                {
                    long ms = sw.ElapsedMilliseconds;
                    totalMs += ms;
                    if (debugLogTiming)
                        Debug.Log($"[Timing] {_steps[i].Name}: {ms}ms");
                }
            }
        }

        _damageNumberManager.Update(_frameDeltaTime);

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        if (cam != null)
        {
            _renderSystem.RenderEnemies(_enemyManager.GetEnemies(), simulationZone, cam);
            _attackDebugRenderer.Render(_attackEntityManager.GetEntities(), simulationZone, cam);
            _damageNumberRenderSystem.Render(_damageNumberManager.GetEntities(), simulationZone, cam);
        }

        if (hasController)
            debugController.NotifyFrameComplete();

        if (debugLogTiming)
            Debug.Log($"[Timing] Total: {totalMs}ms");
    }

    void StepSpawnEnemies()
    {
        List<Vector2> spawnPositions = _spawner.GetSpawnPositions(_simulationTime);
        if (spawnPositions.Count > 0 && enemyAuthoringData != null)
        {
            var positionsInRect = new List<Vector2>(spawnPositions.Count);
            for (int i = 0; i < spawnPositions.Count; i++)
            {
                Vector2 p = spawnPositions[i];
                positionsInRect.Add(new Vector2(rect.xMin, rect.yMin + p.y));
            }
            _enemyManager.CreateEnemies(positionsInRect, enemyAuthoringData);
        }
    }

    void StepMoveEnemies()
    {
        _movementSystem.MoveEnemies(_enemyManager.GetEnemies(), _frameDeltaTime);
    }

    void StepBuildGrid()
    {
        _enemyManager.BuildGrid();
    }

    void StepCull()
    {
        _cullingSystem.CollectEnemiesPastRightEdge(_enemyManager.GetEnemies(), rect.xMax, _toRemove);
    }

    void StepRemoveCulled()
    {
        if (_toRemove.Count > 0)
            _enemyManager.RemoveEnemies(_toRemove);
    }

    void StepAttackTick()
    {
        var attackEntities = _attackEntityManager.GetEntities();
        if (attackEntities.Length > 0)
        {
            _attackTimeSystem.Tick(attackEntities, _frameDeltaTime);
            _attackMovementSystem.MoveEntities(_attackEntityManager.GetEntities(), _frameDeltaTime);
        }
    }

    void StepCollision()
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
            _rawCollisionEvents.Clear();
    }

    void StepDamage()
    {
        _hitEvents.Clear();
        _killEvents.Clear();

        if (_rawCollisionEvents.Length > 0)
        {
            NativeArray<AttackEntity> attackEntities = _attackEntityManager.GetEntities();
            NativeArray<Enemy> enemies = _enemyManager.GetEnemies();
            NativeArray<ChainPolicyRuntime> chainPolicies = _attackEntityManager.GetChainPolicies();

            _attackEntityManager.ValidateParallelLists();
            _hitResolver.Resolve(_rawCollisionEvents, attackEntities, enemies, _attackEntityManager.GetPiercePolicies(), _resolvedHits);
            _attackEntityManager.ValidateHitEvents(_resolvedHits, _enemyManager.EnemyCount);
            _chainSystem.ResolveChains(_resolvedHits, attackEntities, chainPolicies, _enemyManager.Grid, enemies);
            _damageSystem.ProcessHits(_resolvedHits, attackEntities, enemies, _hitEvents, _killEvents);

            SpawnDamageNumbers(attackEntities, enemies);

            _deadEnemyRemovalSystem.CollectDeadEnemies(_enemyManager.GetEnemies(), _toRemove);
            if (_toRemove.Count > 0)
                _enemyManager.RemoveEnemies(_toRemove);
        }
    }

    void StepAttackExpire()
    {
        if (_attackEntityManager.EntityCount == 0) return;

        _attackEntityManager.ValidateParallelLists();
        NativeArray<AttackEntity> entities = _attackEntityManager.GetEntities();
        _pierceSystem.CollectRemovals(entities, _attackEntityManager.GetPiercePolicies(), _attackRemovalEvents);
        _expirationSystem.CollectRemovals(entities, _attackEntityManager.GetExpirationPolicies(), _attackRemovalEvents);
        _chainSystem.CollectRemovals(entities, _attackEntityManager.GetChainPolicies(), _attackRemovalEvents);
        _attackEntityManager.ApplyRemovals(_attackRemovalEvents);
        _attackRemovalEvents.Clear();
    }

    void SpawnDamageNumbers(NativeArray<AttackEntity> attackEntities, NativeArray<Enemy> enemies)
    {
        for (int i = 0; i < _resolvedHits.Length; i++)
        {
            HitEvent hit = _resolvedHits[i];
            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            Enemy enemy = enemies[hit.enemyIndex];

            float total = 0f;
            total += atk.physicalDamage * (enemy.elementalWeakness == DamageType.Physical ? DamageSystem.WeaknessMultiplier : 1f);
            total += atk.coldDamage * (enemy.elementalWeakness == DamageType.Cold ? DamageSystem.WeaknessMultiplier : 1f);
            total += atk.fireDamage * (enemy.elementalWeakness == DamageType.Fire ? DamageSystem.WeaknessMultiplier : 1f);
            total += atk.lightningDamage * (enemy.elementalWeakness == DamageType.Lightning ? DamageSystem.WeaknessMultiplier : 1f);

            if (total > 0f)
                _damageNumberManager.Spawn(hit.hitPosition, (int)total);
        }
    }

    float2 GetCastForward()
    {
        return new float2(1f, 0f);
    }

    static List<Spell> BuildRuntimeSpells(List<SpellAuthoringData> authoringList)
    {
        var list = new List<Spell>();
        if (authoringList == null) return list;
        for (int i = 0; i < authoringList.Count; i++)
        {
            SpellAuthoringData a = authoringList[i];
            if (a == null) continue;
            list.Add(new Spell
            {
                spellId = i,
                baseMultiplier = a.baseMultiplier,
                castCompletionDuration = a.castCompletionDuration,
                castTime = a.castTime,
                attributeMask = a.attributeMask,
                invocationCount = 0,
                roundTimeInvokedAt = 0
            });
        }
        return list;
    }

    void OnDestroy()
    {
        _enemyManager?.Dispose();
        _renderSystem?.Dispose();
        _attackEntityManager?.Dispose();
        _attackDebugRenderer?.Dispose();
        _collisionSystem?.Dispose();
        _chainSystem?.Dispose();
        if (_rawCollisionEvents.IsCreated) _rawCollisionEvents.Dispose();
        if (_resolvedHits.IsCreated) _resolvedHits.Dispose();
        if (_attackRemovalEvents.IsCreated) _attackRemovalEvents.Dispose();
        if (_hitEvents.IsCreated) _hitEvents.Dispose();
        if (_killEvents.IsCreated) _killEvents.Dispose();
        _damageNumberManager?.Dispose();
        _damageNumberRenderSystem?.Dispose();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null) return;
        for (int i = 0; i < _debugDrawables.Count; i++)
            _debugDrawables[i].DrawGizmos(simulationZone);
    }
}
