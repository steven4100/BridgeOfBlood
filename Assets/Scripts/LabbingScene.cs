using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Combat lab: session bootstrap and simulation ownership.
/// Simulation frame coordination (including item evaluation) lives in <see cref="CombatSimulationController"/>.
/// Combat draw/materials live on <see cref="CombatPresentationDriver"/> (bus-driven).
/// Uses <see cref="SimulationConfig.spawner"/> as authored; brush input is wired via <see cref="BrushStrokeSpawnerController.Bind"/> when the simulation spawner is a <see cref="BrushStrokeEnemySpawner"/>.
/// Registers session services from a runtime <see cref="GameConfig"/> copy for inventory/shop UI in the lab scene.
/// </summary>
[DefaultExecutionOrder(-90)]
public class LabbingScene : MonoBehaviour
{
    [Tooltip("Authoring asset on disk. A runtime clone is created via GameConfig.CreateRuntimeCopy.")]
    [SerializeField] GameConfig gameConfig;
    [SerializeField] RectTransform simulationZone;
    [SerializeField] BrushStrokeSpawnerController brushController;
    [SerializeField] CombatPresentationDriver presentationDriver;

    [Header("Brush / audio")]
    [Tooltip("Camera for brush picking. Combat draw camera is on CombatPresentationDriver.")]
    [SerializeField] Camera renderCamera;
    [Tooltip("Optional. Created under this object when unset.")]
    [SerializeField] GameAudioManager gameAudioManager;

    [Header("Player")]
    [SerializeField] float playerMoveSpeed = 100f;

    [Header("Spells & Items")]
    [Tooltip("Default C so Space can toggle play/pause on Simulation Debug Controller.")]
    public KeyCode castInputKey = KeyCode.C;
    public SpellModificationsTestData castModifications;
    [SerializeField] bool debugLogItemEval;

    [Header("Debug")]
    [SerializeField] SimulationDebugController debugController;
    [SerializeField] bool debugLogTiming;

    /// <summary>Session-owned config (wallet, inventory); destroyed on teardown.</summary>
    GameConfig _runtimeConfig;
    CombatSimulationController _combatSimulation;

    public GameSimulation Simulation => _combatSimulation?.Simulation;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _combatSimulation?.LastItemResults;

    void Awake()
    {
        if (gameAudioManager == null)
        {
            var audioRoot = new GameObject("GameAudioManager");
            audioRoot.transform.SetParent(transform, false);
            gameAudioManager = audioRoot.AddComponent<GameAudioManager>();
        }

        CreateRuntimeSession();
    }

    void Update()
    {
        _combatSimulation.Tick(Time.deltaTime, Input.GetKeyDown(castInputKey), int.MaxValue);
    }

    void OnGUI()
    {
        const float pad = 10f;
        const float width = 140f;
        const float height = 28f;
        var rect = new Rect(Screen.width - width - pad, pad, width, height);
        if (GUI.Button(rect, "Reset Simulation"))
            ResetSimulation();
    }

    /// <summary>
    /// Recopies the authored <see cref="gameConfig"/>, rebuilds simulation/casters, and repositions the player.
    /// </summary>
    public void ResetSimulation()
    {
        DisposeRuntimeSession();
        CreateRuntimeSession();
    }

    void CreateRuntimeSession()
    {
        _runtimeConfig = GameConfig.CreateRuntimeCopy(gameConfig);
        PlayerInventory inventory = _runtimeConfig.playerInventory;
        inventory.SpellCollection.ClearRuntimeSpellTracking();
        RegisterSessionServices();

        _combatSimulation = new CombatSimulationController(new CombatSimulationControllerConfig
        {
            RuntimeGameConfig = _runtimeConfig,
            PlayerMoveSpeed = playerMoveSpeed,
            CastModifications = castModifications,
            DebugLogTiming = debugLogTiming,
            DebugLogItemEval = debugLogItemEval,
            DebugController = debugController
        });

        presentationDriver.Bind(_combatSimulation.Simulation.AttackEntityManager);

        if (brushController != null)
            brushController.Bind(simulationZone, renderCamera, _combatSimulation.Simulation.Spawner);

        SharedGameEventBus.Bus.Raise(new RoundEnterEvent
        {
            state = SessionState.Round,
            roundNumber = 0,
            bloodQuota = 0f,
            spellLoopsPerRound = 0,
            playfield = _combatSimulation.Simulation.State.Playfield
        });
    }

    void DisposeRuntimeSession()
    {
        _combatSimulation?.Dispose();
        _combatSimulation = null;
        GameConfig.DestroyRuntimeCopy(_runtimeConfig);
        _runtimeConfig = null;
    }

    void RegisterSessionServices()
    {
        PlayerInventory inventory = _runtimeConfig.playerInventory;
        ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(inventory.SpellCollection);
        ServiceLocator.Current.RegisterInstance<IInventoryService>(inventory);
        ServiceLocator.Current.RegisterInstance<IWalletService>(_runtimeConfig.playerWallet);
        ServiceLocator.Current.RegisterInstance<IShopService>(
            new RepositoryShopService(
                new ShopRepository(_runtimeConfig.shopConfig),
                inventory));
    }

    void OnDestroy()
    {
        DisposeRuntimeSession();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null)
            return;

        Transform zone = simulationZone.transform;
        brushController?.DrawGizmos(zone);
        _combatSimulation?.DrawGizmos(zone);
    }
}
