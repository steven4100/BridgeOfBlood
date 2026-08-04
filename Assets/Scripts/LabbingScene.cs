using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Combat lab bootstrapper: runtime <see cref="GameConfig"/>, ServiceLocator registration,
/// and ownership of <see cref="CombatSimulationController"/> tick/reset/dispose.
/// After registration raises <see cref="ServicesRegisteredEvent"/> so dependents bind from the locator.
/// </summary>
[DefaultExecutionOrder(-110)]
public class LabbingScene : MonoBehaviour
{
    [Tooltip("Authoring asset on disk. A runtime clone is created via GameConfig.CreateRuntimeCopy.")]
    [SerializeField] GameConfig gameConfig;
    [SerializeField] RectTransform simulationZone;
    [Tooltip("Camera for simulation-zone pointer mapping (registered on ISimulationZoneService).")]
    [SerializeField] Camera renderCamera;

    [Header("Player")]
    [SerializeField] float playerMoveSpeed = 100f;

    [Header("Spells & Items")]
    [Tooltip("Default C so Space can toggle play/pause on Simulation Debug Controller.")]
    public KeyCode castInputKey = KeyCode.C;
    public SpellModificationsTestData castModifications;

    [Header("Debug")]
    [SerializeField] SimulationDebugController debugController;

    /// <summary>Session-owned config (wallet, inventory); destroyed on teardown.</summary>
    GameConfig _runtimeConfig;
    CombatSimulationController _combatSimulation;

    void Start()
    {
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

        _combatSimulation = new CombatSimulationController(new CombatSimulationControllerConfig
        {
            RuntimeGameConfig = _runtimeConfig,
            PlayerMoveSpeed = playerMoveSpeed,
            CastModifications = castModifications,
            DebugController = debugController
        });

        RegisterSessionServices();

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
        ServiceLocator.Current.RegisterInstance<ISimulationZoneService>(
            new SimulationZoneService(simulationZone, renderCamera));
        ServiceLocator.Current.RegisterInstance(_combatSimulation);
        ServicesRegisteredEvent.Raise();
    }

    void OnDestroy()
    {
        DisposeRuntimeSession();
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null)
            return;
        _combatSimulation?.DrawGizmos(simulationZone);
    }
}
