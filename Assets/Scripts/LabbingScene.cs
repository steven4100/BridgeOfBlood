using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
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

    [Header("Spells & Items")]
    [Tooltip("Default C so Space can toggle play/pause on Simulation Debug Controller.")]
    public KeyCode castInputKey = KeyCode.C;

    [Header("Debug")]
    [SerializeField] SimulationDebugController debugController;

    /// <summary>Session-owned config (wallet, inventory); destroyed on teardown.</summary>
    GameConfig _runtimeConfig;
    CombatSimulationController _combatSimulation;
    int _labRoundIndex;

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
        const float resetWidth = 140f;
        const float resetHeight = 28f;
        var resetRect = new Rect(Screen.width - resetWidth - pad, pad, resetWidth, resetHeight);
        if (GUI.Button(resetRect, "Reset Simulation"))
            ResetSimulation();

        DrawRoundSwitcher(pad);
    }

    void DrawRoundSwitcher(float pad)
    {
        int roundCount = _runtimeConfig.roundConfigs.Count;
        RoundConfig round = _runtimeConfig.GetRoundConfig(_labRoundIndex + 1);

        const float barWidth = 560f;
        const float barHeight = 56f;
        var bar = new Rect((Screen.width - barWidth) * 0.5f, pad, barWidth, barHeight);
        GUI.Box(bar, GUIContent.none);

        const float btnW = 64f;
        const float btnH = 24f;
        float y = bar.y + 6f;
        if (GUI.Button(new Rect(bar.x + 8f, y, btnW, btnH), "Prev") && _labRoundIndex > 0)
            SetLabRoundIndex(_labRoundIndex - 1);

        GUI.Label(
            new Rect(bar.x + 80f, y, 200f, btnH),
            $"Round {_labRoundIndex + 1} / {roundCount}");

        if (GUI.Button(new Rect(bar.xMax - btnW - 8f, y, btnW, btnH), "Next") && _labRoundIndex < roundCount - 1)
            SetLabRoundIndex(_labRoundIndex + 1);

        GUI.Label(
            new Rect(bar.x + 8f, bar.y + 32f, bar.width - 16f, 20f),
            $"Kill quota: {round.killQuotaPercent:0.#}% of spawned   HP ×{round.enemyHealthMultiplier:0.##}   Speed ×{round.enemyMoveSpeedMultiplier:0.##}");
    }

    void SetLabRoundIndex(int index)
    {
        _labRoundIndex = index;
        ApplyLabRound();
        _combatSimulation.ResetForNewRound();
        RaiseRoundEnter();
        ServicesRegisteredEvent.Raise();
    }

    void ApplyLabRound()
    {
        RoundConfig round = _runtimeConfig.GetRoundConfig(_labRoundIndex + 1);
        _combatSimulation.ApplyRoundConfig(in round);
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
            DebugController = debugController
        });

        ApplyLabRound();
        _combatSimulation.ResetForNewRound();
        RegisterSessionServices();
        RaiseRoundEnter();
    }

    void RaiseRoundEnter()
    {
        SharedGameEventBus.Bus.Raise(new RoundEnterEvent
        {
            state = SessionState.Round,
            roundNumber = _labRoundIndex + 1,
            killQuotaPercent = _runtimeConfig.GetRoundConfig(_labRoundIndex + 1).killQuotaPercent,
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
