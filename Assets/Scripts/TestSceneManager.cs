using System;
using System.Collections.Generic;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Effects;
using Unity.Mathematics;

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
/// Thin scene runner: creates all systems, delegates round simulation to <see cref="RoundController"/>,
/// and uses <see cref="SessionStateMachine"/> to broker high-level transitions (Pregame/Round/Shop/Lose).
/// </summary>
public class TestSceneManager : MonoBehaviour
{
    [Header("Simulation")]
    public float spawnRate = 2f;
    public RectTransform simulationZone;
    Rect rect => simulationZone != null ? simulationZone.rect : default;
    public EnemySpawnTable spawnTable;
    public float gizmoRadius = 5f;

    [Header("Rendering")]
    public Camera renderCamera;
    public Material spriteMaterial;
    public Material attackDebugMaterial;
    public Material damageNumberMaterial;
    public SpriteRenderDatabase spriteRenderDatabase;

    [Header("Player")]
    public float playerMoveSpeed = 100f;
    public PlayerRenderer playerRenderer;

    [Header("Spells & Items")]
    public List<SpellAuthoringData> spells = new List<SpellAuthoringData>();
    public KeyCode castInputKey = KeyCode.Space;
    public SpellModificationsTestData castModifications;
    public List<Item> items = new List<Item>();

    [Header("Round")]
    public RoundConfig roundConfig = new RoundConfig();

    [Header("Debug")]
    public bool debugLogTiming;
    public SimulationDebugController debugController;

    private Player _player;
    private GameSimulation _simulation;
    private LoopedSpellCaster _loopedSpellCaster;
    private SpriteInstancedRenderer _spriteRenderer;
    private SpriteInstanceBuilder _spriteInstanceBuilder;
    private AttackEntityDebugRenderer _attackDebugRenderer;
    private DamageNumberController _damageNumberController;
    private EffectSpriteController _effectSpriteController;
    private TelemetryAggregator _telemetryAggregator;
    private SpellCollection _spellCollection;
    private SessionStateMachine _session;
    private RoundController _roundController;
    private ShopController _shopController;
    private GameState _currentGameState;

    public TelemetryAggregator TelemetryAggregator => _telemetryAggregator;
    public GameState CurrentGameState => _currentGameState;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _roundController?.LastItemResults;
    public GameSimulation Simulation => _simulation;
    public RoundController RoundController => _roundController;
    public SessionStateMachine Session => _session;

    void Start()
    {
        _spriteRenderer = new SpriteInstancedRenderer(spriteMaterial);
        _spriteInstanceBuilder = new SpriteInstanceBuilder(spriteRenderDatabase);
        _damageNumberController = new DamageNumberController(damageNumberMaterial);
        _effectSpriteController = new EffectSpriteController();

        Rect r = rect;
        _player = new Player(
            new float2(r.xMax - 10f, r.center.y),
            playerMoveSpeed);

        if (playerRenderer != null)
            playerRenderer.Player = _player;

        var simConfig = new SimulationConfig
        {
            SimulationZone = simulationZone,
            SpawnRate = spawnRate,
            GizmoRadius = gizmoRadius,
            SpawnTable = spawnTable
        };
        _simulation = new GameSimulation(simConfig);

        var emissionHandler = new SpellEmissionHandler(_simulation.GetAttackEntityManager());
        _spellCollection = new SpellCollection(spells);
        _loopedSpellCaster = new LoopedSpellCaster(_spellCollection.RuntimeSpells, emissionHandler);

        _attackDebugRenderer = new AttackEntityDebugRenderer(_simulation.GetAttackEntityManager(), attackDebugMaterial);
        _telemetryAggregator = new TelemetryAggregator(_spellCollection.Count);

        _session = new SessionStateMachine();

        var roundCfg = new RoundControllerConfig
        {
            castInputKey = castInputKey,
            debugLogTiming = debugLogTiming,
            roundConfig = roundConfig,
            castModifications = castModifications,
            items = items,
            debugController = debugController
        };
        _roundController = new RoundController(
            _player, _simulation, _loopedSpellCaster,
            _telemetryAggregator, _spellCollection,
            _damageNumberController, _effectSpriteController,
            _spriteInstanceBuilder, _spriteRenderer,
            _attackDebugRenderer, roundCfg);
        _shopController = new ShopController();

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);
    }

    void Update()
    {
        switch (_session.CurrentState)
        {
            case SessionState.Pregame:
                UpdatePregame();
                break;

            case SessionState.Round:
                UpdateRound();
                break;

            case SessionState.Shop:
                UpdateShop();
                break;

            case SessionState.Lose:
                UpdateLose();
                break;
        }

        _currentGameState = BuildGameState();
    }

    void UpdatePregame()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (_session.RequestStart())
                ResetForNewRound();
        }
    }

    void UpdateRound()
    {
        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        RoundTickResult result = _roundController.Tick(Time.deltaTime, rect, cam, simulationZone);

        if (result.roundEnded)
            _session.OnRoundEnded(result.quotaMet);
    }

    void UpdateShop()
    {
        ShopTickResult result = _shopController.Tick();
        if (result.requestedNextRound && _session.RequestNextRound())
        {
            _roundController.StartNextRound();
            ResetForNewRound();
        }
    }

    void UpdateLose()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (_session.RequestRetry())
            {
                _roundController.Retry();
                ResetForNewRound();
            }
        }
    }

    void ResetForNewRound()
    {
        _simulation.ResetForNewRound();
        _loopedSpellCaster.Reset();
        _loopedSpellCaster.ClearCastState();
        _player.PlaceAtRightSide(rect);
    }

    GameState BuildGameState()
    {
        var round = _telemetryAggregator.CurrentRound;
        return new GameState
        {
            sessionState = _session.CurrentState,
            phase = _roundController.Phase,
            roundNumber = _roundController.RoundNumber,
            bloodQuota = _roundController.BloodQuota,
            bloodExtracted = _roundController.BloodExtractedThisRound,
            quotaMet = _roundController.QuotaMet,
            spellLoopsPerRound = _roundController.SpellLoopsPerRound,
            loopsCompleted = _loopedSpellCaster.LoopCount,
            roundMetrics = round.aggregate,
            simulationTime = _simulation.SimulationTime,
            enemyCount = _simulation.GetEnemies().Length,
            attackEntityCount = _simulation.GetAttackEntityManager().EntityCount
        };
    }

    void OnDestroy()
    {
        _simulation?.Dispose();
        _spriteRenderer?.Dispose();
        _damageNumberController?.Dispose();
        _effectSpriteController?.Dispose();
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
