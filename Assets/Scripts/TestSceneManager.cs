using System;
using System.Collections.Generic;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Data.Enemies;
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
/// and drives high-level session flow via <see cref="SessionFlowController"/>.
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
    [Tooltip("Authoring asset on disk. A runtime clone is created via GameConfig.CreateRuntimeCopy.")]
    [SerializeField] GameConfig gameConfig;
    public KeyCode castInputKey = KeyCode.Space;
    public SpellModificationsTestData castModifications;

    [Header("Debug")]
    public bool debugLogTiming;
    public SimulationDebugController debugController;

    [Header("UI")]
    [SerializeField] ShopPanelPresenter shopPanel;
    [SerializeField] RoundPanelPresenter roundPanel;

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
    private SessionFlowController _sessionFlow;
    private RoundController _roundController;
    private ShopController _shopController;
    private GameState _currentGameState;
    /// <summary>Session-owned config (wallet, inventory, round tuning); destroyed when rebuilding session.</summary>
    GameConfig _runtimeGameConfig;

    public TelemetryAggregator TelemetryAggregator => _telemetryAggregator;
    public GameState CurrentGameState => _currentGameState;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _roundController?.LastItemResults;
    public GameSimulation Simulation => _simulation;
    public RoundController RoundController => _roundController;
    public SessionFlowController SessionFlow => _sessionFlow;

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

        CreateRuntimeGameConfigCopy();

        _spellCollection = new SpellCollection(null);
        _spellCollection.SyncSpellLoopFromInventory(_runtimeGameConfig.playerInventory.GetSpellLoopAuthoring());
        _loopedSpellCaster = new LoopedSpellCaster(_spellCollection.RuntimeSpells, emissionHandler);

        _attackDebugRenderer = new AttackEntityDebugRenderer(_simulation.GetAttackEntityManager(), attackDebugMaterial);
        int initialSpellCount = Mathf.Max(8, _spellCollection.Count);
        _telemetryAggregator = new TelemetryAggregator(initialSpellCount);

        _shopController = new ShopController();

        var roundCfg = new RoundControllerConfig
        {
            castInputKey = castInputKey,
            debugLogTiming = debugLogTiming,
            gameConfig = _runtimeGameConfig,
            castModifications = castModifications,
            debugController = debugController
        };
        _roundController = new RoundController(
            _player, _simulation, _loopedSpellCaster,
            _telemetryAggregator,
            _damageNumberController, _effectSpriteController,
            _spriteInstanceBuilder, _spriteRenderer,
            _attackDebugRenderer, roundCfg);

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);

        var sessionContext = new SessionFlowContext(
            () => _runtimeGameConfig,
            _roundController,
            shopPanel,
            _shopController,
            _spellCollection,
            ResetForNewRound,
            CreateRuntimeGameConfigCopy,
            () => rect,
            simulationZone,
            () => renderCamera != null ? renderCamera : Camera.main);

        _sessionFlow = new SessionFlowController(sessionContext,
            new PregameSessionPhase(NoOpStatePresenter.Instance),
            new RoundSessionPhase(roundPanel),
            new ShopSessionPhase(shopPanel),
            new LoseSessionPhase(NoOpStatePresenter.Instance));
    }

    void Update()
    {
        _sessionFlow.Tick(Time.deltaTime);
        _currentGameState = BuildGameState();
    }

    /// <summary>
    /// Replaces <see cref="_runtimeGameConfig"/> with a new <see cref="GameConfig.CreateRuntimeCopy"/> of the serialized template.
    /// </summary>
    void CreateRuntimeGameConfigCopy()
    {
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = GameConfig.CreateRuntimeCopy(gameConfig);
    }

    void ResetForNewRound()
    {
        // Not per-frame: spell loop sync + runtime counters; full rebuild only if inventory spells changed.
        _spellCollection.SyncSpellLoopFromInventory(_runtimeGameConfig.playerInventory.GetSpellLoopAuthoring());
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
            sessionState = _sessionFlow.CurrentState,
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
        _sessionFlow?.Shutdown();
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = null;
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
