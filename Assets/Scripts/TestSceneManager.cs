using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

/// <summary>
/// A named simulation step that can be executed, timed, and stepped through by the debug controller.
/// </summary>
public struct SimulationStepCommand
{
    public string Name;
    public Action Execute;
}

public struct ItemEvalResult
{
    public string itemName;
    public bool applied;
}

/// <summary>
/// Scene runner: owns the simulation zone, player, input, rendering, and debug controls.
/// Delegates all simulation logic to GameSimulation.
/// </summary>
public class TestSceneManager : MonoBehaviour
{
    public float spawnRate = 2f;
    public RectTransform simulationZone;
    Rect rect => simulationZone != null ? simulationZone.rect : default;
    public EnemySpawnTable spawnTable;
    public Camera renderCamera;
    public Material spriteMaterial;
    public Material attackDebugMaterial;
    public Material damageNumberMaterial;
    public SpriteRenderDatabase spriteRenderDatabase;
    public float playerMoveSpeed = 100f;
    public PlayerRenderer playerRenderer;
    public List<SpellAuthoringData> spells = new List<SpellAuthoringData>();
    public KeyCode castInputKey = KeyCode.Space;
    public SpellModificationsTestData castModifications;
    public float gizmoRadius = 5f;
    public bool debugLogTiming;
    public SimulationDebugController debugController;
    public List<Item> items = new List<Item>();

    private Player _player;
    private GameSimulation _simulation;
    private LoopedSpellCaster _loopedSpellCaster;
    private SpriteInstancedRenderer _spriteRenderer;
    private SpriteInstanceBuilder _spriteInstanceBuilder;
    private AttackEntityDebugRenderer _attackDebugRenderer;
    private DamageNumberController _damageNumberController;
    private TelemetryAggregator _telemetryAggregator;
    private SpellCollection _spellCollection;
    private readonly EffectContext _effectContext = new EffectContext();
    private readonly List<ItemEvalResult> _lastItemResults = new List<ItemEvalResult>();

    public TelemetryAggregator TelemetryAggregator => _telemetryAggregator;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _lastItemResults;
    public GameSimulation Simulation => _simulation;

    void Start()
    {
        _spriteRenderer = new SpriteInstancedRenderer(spriteMaterial);
        _spriteInstanceBuilder = new SpriteInstanceBuilder(spriteRenderDatabase);
        _damageNumberController = new DamageNumberController(damageNumberMaterial);

        Rect r = rect;
        _player = new Player(
            new float2(r.center.x, r.center.y),
            playerMoveSpeed);

        if (playerRenderer != null)
            playerRenderer.Player = _player;

        var config = new SimulationConfig
        {
            SimulationZone = simulationZone,
            SpawnRate = spawnRate,
            GizmoRadius = gizmoRadius,
            SpawnTable = spawnTable
        };
        _simulation = new GameSimulation(config);

        var emissionHandler = new SpellEmissionHandler(_simulation.GetAttackEntityManager());
        _spellCollection = new SpellCollection(spells);
        _loopedSpellCaster = new LoopedSpellCaster(_spellCollection.RuntimeSpells, emissionHandler);

        _attackDebugRenderer = new AttackEntityDebugRenderer(_simulation.GetAttackEntityManager(), attackDebugMaterial);
        _telemetryAggregator = new TelemetryAggregator(_spellCollection.Count);

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);
    }

    void Update()
    {
        bool hasController = debugController != null;
        if (hasController)
            debugController.ProcessInput();

        _player.Update(Time.deltaTime, rect);

        bool castRequested = Input.GetKeyDown(castInputKey);
        var mods = castModifications != null ? castModifications.GetModifications() : new SpellModifications();

        EvaluateItems(mods);

        SpellCastResult castResult = _loopedSpellCaster.AttemptToCastNextSpell(_simulation.SimulationTime, _player.Position, _spellCollection.AuthoringData, castRequested, mods);
        _loopedSpellCaster.Update(_simulation.SimulationTime, -1 * GetCastForward());

        bool advanceTime = !hasController || debugController.ShouldAdvanceTime;
        if (advanceTime)
        {
            float dt = hasController ? debugController.DeltaTime : Time.deltaTime;
            _simulation.AdvanceTime(dt);
        }

        Stopwatch sw = debugLogTiming ? new Stopwatch() : null;
        long totalMs = 0;

        for (int i = 0; i < _simulation.StepCount; i++)
        {
            if (!hasController || debugController.ShouldRunPhase(i, _simulation.GetStepName(i)))
            {
                sw?.Restart();
                _simulation.ExecuteStep(i);
                if (sw != null)
                {
                    long ms = sw.ElapsedMilliseconds;
                    totalMs += ms;
                    if (debugLogTiming)
                        Debug.Log($"[Timing] {_simulation.GetStepName(i)}: {ms}ms");
                }
            }
        }

        float frameDt = hasController ? debugController.DeltaTime : Time.deltaTime;
        _telemetryAggregator.ProcessFrame(
            _simulation.GetDamageEvents(),
            _simulation.GetStatusAilmentAppliedEvents(),
            frameDt,
            _simulation.SimulationTime,
            castResult);

        _damageNumberController.SpawnFromDamageEvents(_simulation.GetDamageEvents(), _simulation.GetEnemies());
        _simulation.ClearDamageEvents();
        _simulation.ClearStatusAilmentAppliedEvents();

        if (advanceTime)
            _damageNumberController.Update(hasController ? debugController.DeltaTime : Time.deltaTime);

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        _spriteInstanceBuilder.Build(_simulation.GetEnemies(), _simulation.GetAttackEntities());
        _spriteRenderer.Render(_spriteInstanceBuilder.Buffer, _spriteInstanceBuilder.Count, simulationZone, cam);
        _attackDebugRenderer.Render(_simulation.GetAttackEntities(), simulationZone, cam);
        _damageNumberController.Render(simulationZone, cam);

        if (hasController)
            debugController.NotifyFrameComplete();

        if (debugLogTiming)
            Debug.Log($"[Timing] Total: {totalMs}ms");
    }

    void EvaluateItems(SpellModifications mods)
    {
        _effectContext.frameMetrics = _telemetryAggregator.CurrentFrame.aggregate;
        _effectContext.spellCastMetrics = _telemetryAggregator.CurrentSpellCast.aggregate;
        _effectContext.spellLoopMetrics = _telemetryAggregator.CurrentSpellLoop.aggregate;
        _effectContext.roundMetrics = _telemetryAggregator.CurrentRound.aggregate;
        _effectContext.gameMetrics = _telemetryAggregator.Game.aggregate;
        _effectContext.spellModifications = mods;

        _lastItemResults.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;

            _lastItemResults.Add(new ItemEvalResult
            {
                itemName = item.name,
                applied = item.Apply(_effectContext)
            });
        }
    }

    float2 GetCastForward()
    {
        return new float2(1f, 0f);
    }

    void OnDestroy()
    {
        _simulation?.Dispose();
        _spriteRenderer?.Dispose();
        _damageNumberController?.Dispose();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null || _simulation == null) return;
        var drawables = _simulation.GetDebugDrawables();
        for (int i = 0; i < drawables.Count; i++)
            drawables[i].DrawGizmos(simulationZone);
        _attackDebugRenderer?.DrawGizmos(simulationZone);
    }
}
