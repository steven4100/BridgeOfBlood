using System;
using System.Collections.Generic;
using EZServiceLocation;
using UnityEngine;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;

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
public class GameManager : MonoBehaviour
{
    public RectTransform simulationZone;

    [Header("Presentation")]
    [Tooltip("Camera for simulation-zone pointer mapping (registered on ISimulationZoneService).")]
    [SerializeField] Camera renderCamera;

    [Header("Spells & Items")]
    [Tooltip("Authoring asset on disk. A runtime clone is created via GameConfig.CreateRuntimeCopy.")]
    [SerializeField] GameConfig gameConfig;

    [Header("Debug")]
    public SimulationDebugController debugController;

    [Header("Map Modifiers")]
    [SerializeField] MapModifierPlacementHost mapModifierPlacementHost;

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
        var audioRoot = new GameObject("GameAudioManager");
        audioRoot.transform.SetParent(transform, false);
        audioRoot.AddComponent<GameAudioManager>();
        

        _combatSimulation = new CombatSimulationController(new CombatSimulationControllerConfig
        {
            RuntimeGameConfig = _runtimeGameConfig,
            DebugController = debugController
        });
        ServiceLocator.Current.RegisterInstance(_combatSimulation);
        ServiceLocator.Current.RegisterInstance<ISpellModificationService>(_combatSimulation);
        ServicesRegisteredEvent.Raise();

        _sessionFlow = new SessionFlowController();

        _roundController = new RoundController(
            _combatSimulation,
            null,
            _sessionFlow);

        _sessionFlow.AddRound(_roundController);
        _sessionFlow.AddShop(new ShopSessionPhase(_sessionFlow, () => _runtimeGameConfig));
        _sessionFlow.AddMapMod(
            new MapModSessionPhase(_sessionFlow, _combatSimulation, mapModifierPlacementHost));
        _sessionFlow.EnterShop(new ShopSessionPhase.Context());
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
        ServiceLocator.Current.RegisterInstance<ISimulationZoneService>(
            new SimulationZoneService(simulationZone, renderCamera));
        ServiceLocator.Current.RegisterInstance<IMapModifierSessionService>(
            new MapModifierSessionService());
        return _runtimeGameConfig;
    }

    GameState BuildGameState()
    {
        var sim = _combatSimulation.Simulation.State;
        return new GameState
        {
            sessionState = _sessionFlow.CurrentState,
            phase = _roundController.Phase,
            roundNumber = _roundController.RoundNumber,
            killQuotaPercent = _roundController.KillQuotaPercent,
            minEnemiesKilled = _roundController.MinEnemiesKilled,
            enemiesSpawnedThisRound = sim.EnemiesSpawnedThisRound,
            bloodExtracted = _roundController.BloodExtractedThisRound,
            quotaMet = _roundController.QuotaMet,
            spellLoopsPerRound = _roundController.SpellLoopsPerRound,
            loopsCompleted = _combatSimulation.LoopedSpellCaster.LoopCount,
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
