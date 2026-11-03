using System;
using System.Collections.Generic;
using EZServiceLocation;
using UnityEngine;
using UnityEngine.Serialization;
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

    [SerializeField] Camera renderCamera;
    [SerializeField] GameConfig gameConfig;
    [SerializeField] SimulationDebugController debugController;
    [SerializeField, FormerlySerializedAs("mapModifierPlacementHost")]
    MapModifierController mapModifierController;


    Map map;
    CombatSimulationController _combatSimulation;
    SessionFlowController _sessionFlow;
    RoundController _roundController;
    GameState _currentGameState;
    /// <summary>Session-owned config (wallet, inventory, round tuning); destroyed when rebuilding session.</summary>
    GameConfig _runtimeGameConfig;

    public GameState CurrentGameState => _currentGameState;
    public GameSimulation Simulation => _combatSimulation?.Simulation;

   
    void Start()
    {
        var audioRoot = new GameObject("GameAudioManager");
        audioRoot.transform.SetParent(transform, false);
        audioRoot.AddComponent<GameAudioManager>();

        _runtimeGameConfig = GameConfig.CreateRuntimeCopy(gameConfig);
        map = new Map(_runtimeGameConfig.simulationSize.x, _runtimeGameConfig.simulationSize.y);

        _combatSimulation = new CombatSimulationController(new CombatSimulationControllerConfig
        {
            RuntimeGameConfig = _runtimeGameConfig,
            DebugController = debugController
        });

        _sessionFlow = PrepareStateMachine();
        RegisterServices();
        ServicesRegisteredEvent.Raise();
    }

    void Update()
    {
        _sessionFlow.Tick(Time.deltaTime);
        _currentGameState = BuildGameState();
    }

    SessionFlowController PrepareStateMachine()
    {
        SessionFlowController sessionFlow = new SessionFlowController();

        _roundController = new RoundController(
            _combatSimulation,
            null,
            sessionFlow);

        sessionFlow.AddRound(_roundController);
        sessionFlow.AddShop(new ShopSessionPhase(sessionFlow, () => _runtimeGameConfig));
        sessionFlow.AddMapMod(
            new MapModSessionPhase(sessionFlow, _combatSimulation, mapModifierController));
        sessionFlow.EnterShop(new ShopSessionPhase.Context());
        return sessionFlow;
    }

    GameConfig RegisterServices()
    {
        ServiceLocator.Current.RegisterInstance(_combatSimulation);
        ServiceLocator.Current.RegisterInstance<ISpellModificationService>(_combatSimulation);
        ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(_runtimeGameConfig.playerInventory.SpellCollection);
        ServiceLocator.Current.RegisterInstance<IInventoryService>(_runtimeGameConfig.playerInventory);
        ServiceLocator.Current.RegisterInstance<IWalletService>(_runtimeGameConfig.playerWallet);
        ServiceLocator.Current.RegisterInstance<IShopService>(
            new RepositoryShopService(
                new ShopRepository(_runtimeGameConfig.shopConfig),
                _runtimeGameConfig.playerInventory));
        ServiceLocator.Current.RegisterInstance<IMapService>(
            new MapService(map, renderCamera));
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
}
