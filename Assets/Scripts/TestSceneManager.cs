using System;
using System.Collections.Generic;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Inventory;
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
    [SerializeField] GameAudioManager gameAudioManager;

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
    [SerializeField] SpellInventoryController spellInventory;
    [SerializeField] ItemInventoryController itemInventory;

    private Player _player;
    private GameSimulation _simulation;
    private LoopedSpellCaster _loopedSpellCaster;
    private SpriteInstancedRenderer _spriteRenderer;
    private SpriteInstanceBuilder _spriteInstanceBuilder;
    private AttackEntityDebugRenderer _attackDebugRenderer;
    private DamageNumberController _damageNumberController;
    private EffectSpriteController _effectSpriteController;
    private TelemetryAggregator _telemetryAggregator;
    private SessionFlowController _sessionFlow;
    private RoundController _roundController;
    private GameState _currentGameState;
    private EnemyEmissionTargetProvider _emissionTargetProvider;
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
        if (gameAudioManager == null)
        {
            var audioRoot = new GameObject("GameAudioManager");
            audioRoot.transform.SetParent(transform, false);
            gameAudioManager = audioRoot.AddComponent<GameAudioManager>();
        }

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

        _emissionTargetProvider = new EnemyEmissionTargetProvider(_simulation.EnemyManager);
        var emissionHandler = new SpellEmissionHandler(_simulation.AttackEntityManager, _emissionTargetProvider);

        CreateRuntimeGameConfigCopy();

        PlayerInventory inv = _runtimeGameConfig.playerInventory;
        _loopedSpellCaster = new LoopedSpellCaster(inv.SpellCollection, emissionHandler);

        _attackDebugRenderer = new AttackEntityDebugRenderer(_simulation.AttackEntityManager, attackDebugMaterial);
        int initialSpellCount = Mathf.Max(8, inv.SpellCollection.Count);
        _telemetryAggregator = new TelemetryAggregator(initialSpellCount);

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
            gameAudioManager,
            _spriteInstanceBuilder, _spriteRenderer,
            _attackDebugRenderer,
            roundCfg,
            null,
            roundPanel,
            renderCamera != null ? renderCamera : Camera.main);

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);

        var sessionContext = new SessionFlowContext(
            _runtimeGameConfig,
            _roundController,
            shopPanel,
            inv.SpellCollection,
            simulationZone);

        _sessionFlow = new SessionFlowController(sessionContext,
            new PregameSessionPhase(NoOpStatePresenter.Instance),
            _roundController,
            new ShopSessionPhase(shopPanel),
            new LoseSessionPhase(NoOpStatePresenter.Instance, CreateRuntimeGameConfigCopy));
    }

    void Update()
    {
        _sessionFlow.Tick(Time.deltaTime);
        _currentGameState = BuildGameState();
    }

    /// <summary>
    /// Replaces <see cref="_runtimeGameConfig"/> with a new <see cref="GameConfig.CreateRuntimeCopy"/> of the serialized template,
    /// then re-injects the resulting <see cref="SpellCollection"/> and <see cref="PlayerInventory"/> into the spell and item inventory UIs
    /// so they follow the new instance.
    /// </summary>
    GameConfig CreateRuntimeGameConfigCopy()
    {
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = GameConfig.CreateRuntimeCopy(gameConfig);
        spellInventory.Initialize(_runtimeGameConfig.playerInventory.SpellCollection);
        itemInventory.Initialize(_runtimeGameConfig.playerInventory);
        return _runtimeGameConfig;
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
            simulationTime = _simulation.State.SimulationTime,
            enemyCount = _simulation.State.EnemyCount,
            attackEntityCount = _simulation.State.AttackEntityCount
        };
    }

    void OnDestroy()
    {
        _sessionFlow?.Shutdown();
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = null;
        _simulation?.Dispose();
        _emissionTargetProvider?.Dispose();
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
