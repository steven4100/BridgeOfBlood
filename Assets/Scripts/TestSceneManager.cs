using System;
using System.Collections.Generic;
using EZServiceLocation;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;

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

    [Header("Presentation")]
    [SerializeField] CombatPresentationDriver presentationDriver;
    [Tooltip("Scene-bound combat audio component. Materials/atlas live on CombatPresentationDriver.")]
    [SerializeField] GameAudioManager gameAudioManager;

    [Header("Player")]
    public float playerMoveSpeed = 100f;

    [Header("Spells & Items")]
    [Tooltip("Authoring asset on disk. A runtime clone is created via GameConfig.CreateRuntimeCopy.")]
    [SerializeField] GameConfig gameConfig;
    public KeyCode castInputKey = KeyCode.Space;
    public SpellModificationsTestData castModifications;

    [Header("Debug")]
    public bool debugLogTiming;
    public SimulationDebugController debugController;

    CombatSimulationController _combatSimulation;
    SessionFlowController _sessionFlow;
    RoundController _roundController;
    GameState _currentGameState;
    /// <summary>Session-owned config (wallet, inventory, round tuning); destroyed when rebuilding session.</summary>
    GameConfig _runtimeGameConfig;

    public TelemetryAggregator TelemetryAggregator => _combatSimulation?.TelemetryAggregator;
    public GameState CurrentGameState => _currentGameState;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _roundController?.LastItemResults;
    public GameSimulation Simulation => _combatSimulation?.Simulation;
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

        PlayerInventory inv = _runtimeGameConfig.playerInventory;

        _combatSimulation = new CombatSimulationController(new CombatSimulationControllerConfig
        {
            RuntimeGameConfig = _runtimeGameConfig,
            PlayerMoveSpeed = playerMoveSpeed,
            CastModifications = castModifications,
            DebugLogTiming = debugLogTiming,
            DebugController = debugController
        });

        presentationDriver.Bind(_combatSimulation.Simulation.AttackEntityManager);

        var sessionContext = new SessionFlowContext(
           _runtimeGameConfig,
           _roundController,
           inv.SpellCollection,
           simulationZone);

        _sessionFlow = new SessionFlowController(sessionContext);

        _roundController = new RoundController(
            _combatSimulation,
            new RoundControllerConfig
            {
                castInputKey = castInputKey
            },
            null,
            _sessionFlow);

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
        var round = _combatSimulation.TelemetryAggregator.CurrentRound;
        var sim = _combatSimulation.Simulation.State;
        return new GameState
        {
            sessionState = _sessionFlow.CurrentState,
            phase = _roundController.Phase,
            roundNumber = _roundController.RoundNumber,
            bloodQuota = _roundController.BloodQuota,
            bloodExtracted = _roundController.BloodExtractedThisRound,
            quotaMet = _roundController.QuotaMet,
            spellLoopsPerRound = _roundController.SpellLoopsPerRound,
            loopsCompleted = _combatSimulation.LoopedSpellCaster.LoopCount,
            roundMetrics = round.aggregate,
            simulationTime = sim.SimulationTime,
            enemyCount = sim.EnemyCount,
            attackEntityCount = sim.AttackEntityCount
        };
    }

    void OnDestroy()
    {
        _sessionFlow?.Shutdown();
        GameConfig.DestroyRuntimeCopy(_runtimeGameConfig);
        _runtimeGameConfig = null;
        _combatSimulation?.Dispose();
        _combatSimulation = null;
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null)
            return;
        _combatSimulation?.DrawGizmos(simulationZone);
    }
}
