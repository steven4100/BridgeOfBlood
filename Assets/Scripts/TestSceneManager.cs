using System;
using System.Collections.Generic;
using EZServiceLocation;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
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

[DefaultExecutionOrder(-40)]
public class TestSceneManager : MonoBehaviour
{
    public RectTransform simulationZone;
    Rect rect => simulationZone != null ? simulationZone.rect : default;

    [Header("Rendering")]
    public Camera renderCamera;
    [Tooltip("Scene-bound combat audio component. Asset references (materials, sprite atlas DB) live on GameConfig.presentationResources.")]
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

    private Player _player;
    private GameSimulation _simulation;
    private LoopedSpellCaster _loopedSpellCaster;
    private CombatPresentationLayer _presentation;
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

    void Awake()
    {
        CreateRuntimeGameConfigCopy();
    }

    void Start()
    {
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

        SimulationConfig simConfig = _runtimeGameConfig.simulationConfig;
        simConfig.SimulationZone = simulationZone;
        if (simConfig.spawner is EnemySpawner rateSpawner)
            rateSpawner.spawnLineHeight = rect.height;
        _simulation = new GameSimulation(simConfig);

        _emissionTargetProvider = new EnemyEmissionTargetProvider(_simulation.EnemyManager);
        var emissionHandler = new SpellEmissionHandler(_simulation.AttackEntityManager, _emissionTargetProvider);

        PlayerInventory inv = _runtimeGameConfig.playerInventory;
        _loopedSpellCaster = new LoopedSpellCaster(inv.SpellCollection, emissionHandler);

        _presentation = new CombatPresentationLayer(
            _runtimeGameConfig.presentationResources,
            gameAudioManager,
            _simulation.AttackEntityManager);
        _presentation.BindPlayer(playerRenderer, _player);

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
        var sessionContext = new SessionFlowContext(
           _runtimeGameConfig,
           _roundController,
           inv.SpellCollection,
           simulationZone);

        _sessionFlow = new SessionFlowController(sessionContext);

        _roundController = new RoundController(
            _player, _simulation, _loopedSpellCaster,
            _telemetryAggregator,
            _presentation,
            roundCfg,
            null,
            _sessionFlow);

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);


        _sessionFlow.AddPhase(_roundController, SessionState.Round);
        _sessionFlow.AddPhase(new PregameSessionPhase(_sessionFlow), SessionState.Pregame);
        _sessionFlow.AddPhase(new ShopSessionPhase(_sessionFlow), SessionState.Shop);
        SessionFlow.AddPhase(new LoseSessionPhase(_sessionFlow, CreateRuntimeGameConfigCopy), SessionState.Lose);
    }

    void Update()
    {
        _sessionFlow.Tick(Time.deltaTime);
        _currentGameState = BuildGameState();
    }

    /// <summary>
    /// Replaces <see cref="_runtimeGameConfig"/> with a new <see cref="GameConfig.CreateRuntimeCopy"/> of the serialized template,
    /// then re-registers session services so inventory and shop UIs follow the new instance.
    /// </summary>
    GameConfig CreateRuntimeGameConfigCopy()
    {
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = GameConfig.CreateRuntimeCopy(gameConfig);
        ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(_runtimeGameConfig.playerInventory.SpellCollection);
        ServiceLocator.Current.RegisterInstance<IInventoryService>(_runtimeGameConfig.playerInventory);
        ServiceLocator.Current.RegisterInstance<IWalletService>(_runtimeGameConfig.playerWallet);
        ServiceLocator.Current.RegisterInstance<IShopService>(
            new RepositoryShopService(
                new ShopRepository(_runtimeGameConfig.shopConfig),
                _runtimeGameConfig.playerInventory));
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
        _presentation?.Dispose();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null || _simulation == null) return;
        var drawables = _simulation.GetDebugDrawables();
        for (int i = 0; i < drawables.Count; i++)
            drawables[i].DrawGizmos(simulationZone);
        _presentation?.DrawGizmos(simulationZone);
    }
}
