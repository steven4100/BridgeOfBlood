using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Debug = UnityEngine.Debug;

public class TestSceneManager : MonoBehaviour
{
    private const int TotalSimulationPhases = 5;

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

    [Tooltip("Size of each enemy quad in rect-local units")]
    public float enemyScale = 10f;

    [Header("Debug")]
    [Tooltip("Gizmo sphere radius at each unit position (Scene view, Play mode only)")]
    public float gizmoRadius = 5f;

    [Tooltip("When enabled, logs timing of main Update steps to the console every frame.")]
    public bool debugLogTiming;

    [Tooltip("Optional debug controller for frame/phase stepping. If null, simulation runs normally.")]
    public SimulationDebugController debugController;

    private EnemyManager _enemyManager;
    private EnemyMovementSystemLinear _movementSystem;
    private EnemyCullingSystem _cullingSystem;
    private EnemySpawner _spawner;
    private EnemyRenderSystem _renderSystem;
    private readonly List<IDebugDrawable> _debugDrawables = new List<IDebugDrawable>();
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

        _debugDrawables.Add(_enemyManager.Grid);
        _debugDrawables.Add(new EnemyManagerGizmoDrawer(_enemyManager, gizmoRadius));

        if (debugController != null)
            debugController.Initialize(TotalSimulationPhases);
    }

    void Update()
    {
        if (simulationZone == null) return;

        bool hasController = debugController != null;
        if (hasController)
            debugController.ProcessInput();

        bool advanceTime = !hasController || debugController.ShouldAdvanceTime;
        if (advanceTime)
        {
            _frameDeltaTime = hasController ? debugController.DeltaTime : Time.deltaTime;
            _simulationTime += _frameDeltaTime;
        }

        var sw = debugLogTiming ? Stopwatch.StartNew() : null;
        long spawnMs = 0, moveMs = 0, buildGridMs = 0, cullMs = 0, removeMs = 0;

        int phase = 0;

        if (!hasController || debugController.ShouldRunPhase(phase, "Spawn"))
        {
            sw?.Restart();
            SpawnEnemies();
            if (sw != null) spawnMs = sw.ElapsedMilliseconds;
        }
        phase++;

        if (!hasController || debugController.ShouldRunPhase(phase, "Move"))
        {
            sw?.Restart();
            MoveEnemies(_enemyManager.GetEnemies());
            if (sw != null) moveMs = sw.ElapsedMilliseconds;
        }
        phase++;

        if (!hasController || debugController.ShouldRunPhase(phase, "BuildGrid"))
        {
            sw?.Restart();
            _enemyManager.BuildGrid();
            if (sw != null) buildGridMs = sw.ElapsedMilliseconds;
        }
        phase++;

        if (!hasController || debugController.ShouldRunPhase(phase, "Cull"))
        {
            sw?.Restart();
            _cullingSystem.CollectEnemiesPastRightEdge(_enemyManager.GetEnemies(), rect.xMax, _toRemove);
            if (sw != null) cullMs = sw.ElapsedMilliseconds;
        }
        phase++;

        if (!hasController || debugController.ShouldRunPhase(phase, "Remove"))
        {
            sw?.Restart();
            if (_toRemove.Count > 0)
                _enemyManager.RemoveEnemies(_toRemove);
            if (sw != null) removeMs = sw.ElapsedMilliseconds;
        }
        phase++;

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        if (cam != null)
            _renderSystem.RenderEnemies(_enemyManager.GetEnemies(), simulationZone, cam);

        if (hasController)
            debugController.NotifyFrameComplete();

        if (debugLogTiming)
        {
            long totalMs = spawnMs + moveMs + buildGridMs + cullMs + removeMs;
            Debug.Log($"[Timing] Spawn:{spawnMs}ms Move:{moveMs}ms BuildGrid:{buildGridMs}ms Cull:{cullMs}ms Remove:{removeMs}ms Total:{totalMs}ms");
        }
    }

    void SpawnEnemies()
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

    void MoveEnemies(NativeArray<Enemy> enemies)
    {
        _movementSystem.MoveEnemies(enemies, _frameDeltaTime);
    }

    void OnDestroy()
    {
        _enemyManager?.Dispose();
        _renderSystem?.Dispose();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null) return;
        for (int i = 0; i < _debugDrawables.Count; i++)
            _debugDrawables[i].DrawGizmos(simulationZone);
    }
}
