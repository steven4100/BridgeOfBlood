using System.Collections.Generic;
using System.Diagnostics;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Combat lab: brush painting, looped spell casting, item modifiers, and combat presentation.
/// Registers session services from <see cref="gameConfig"/> for inventory/shop UI in the lab scene.
/// </summary>
[DefaultExecutionOrder(-90)]
public class LabbingScene : MonoBehaviour
{
    [SerializeField] GameConfig gameConfig;
    [SerializeField] RectTransform simulationZone;
    [SerializeField] BrushStrokeSpawnerController brushController;

    [Header("Rendering")]
    [SerializeField] Camera renderCamera;
    [Tooltip("Optional. Created under this object when unset.")]
    [SerializeField] GameAudioManager gameAudioManager;

    [Header("Player")]
    [SerializeField] PlayerRenderer playerRenderer;
    [SerializeField] float playerMoveSpeed = 100f;

    [Header("Spells & Items")]
    [Tooltip("Default C so Space can toggle play/pause on Simulation Debug Controller.")]
    public KeyCode castInputKey = KeyCode.C;
    public SpellModificationsTestData castModifications;
    [SerializeField] bool debugLogItemEval;

    [Header("Debug")]
    [SerializeField] SimulationDebugController debugController;
    [SerializeField] bool debugLogTiming;

    GameConfig _runtimeConfig;
    GameSimulation _simulation;
    CombatPresentationLayer _presentation;
    LoopedSpellCaster _loopedSpellCaster;
    TelemetryAggregator _telemetryAggregator;
    EnemyEmissionTargetProvider _emissionTargetProvider;
    Player _player;
    BrushStrokeEnemySpawner _brushSpawner;
    readonly EffectContext _effectContext = new EffectContext();
    readonly List<ItemEvalResult> _lastItemResults = new List<ItemEvalResult>();

    static readonly float2 CastForward = new float2(-1f, 0f);

    public GameSimulation Simulation => _simulation;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _lastItemResults;

    void Awake()
    {
        _runtimeConfig = gameConfig;
        SimulationConfig simConfig = _runtimeConfig.simulationConfig;
        simConfig.SimulationZone = simulationZone;

        PlayerInventory inventory = _runtimeConfig.playerInventory;
        inventory.RebuildFromStartingDefinition();
        inventory.SpellCollection.ClearRuntimeSpellTracking();
        RegisterSessionServices();

        _brushSpawner = simConfig.spawner as BrushStrokeEnemySpawner;
        if (_brushSpawner == null)
        {
            _brushSpawner = new BrushStrokeEnemySpawner();
            simConfig.spawner = _brushSpawner;
        }

        _simulation = new GameSimulation(simConfig);

        if (brushController != null)
        {
            brushController.SetBrushSpawner(_brushSpawner);
            brushController.Bind(simulationZone, renderCamera);
        }

        if (gameAudioManager == null)
        {
            var audioRoot = new GameObject("GameAudioManager");
            audioRoot.transform.SetParent(transform, false);
            gameAudioManager = audioRoot.AddComponent<GameAudioManager>();
        }

        Rect playfield = simulationZone != null ? simulationZone.rect : default;
        _player = new Player(
            new float2(playfield.xMax - 10f, playfield.center.y),
            playerMoveSpeed);

        _emissionTargetProvider = new EnemyEmissionTargetProvider(_simulation.EnemyManager);
        var emissionHandler = new SpellEmissionHandler(_simulation.AttackEntityManager, _emissionTargetProvider);
        _loopedSpellCaster = new LoopedSpellCaster(inventory.SpellCollection, emissionHandler);

        _presentation = new CombatPresentationLayer(
            _runtimeConfig.presentationResources,
            gameAudioManager,
            _simulation.AttackEntityManager);
        _presentation.BindPlayer(playerRenderer, _player);

        int initialSpellCount = Mathf.Max(8, inventory.SpellCollection.Count);
        _telemetryAggregator = new TelemetryAggregator(initialSpellCount);

        if (debugController != null)
            debugController.Initialize(_simulation.StepCount);
    }

    void Update()
    {
        TickSimulation(Time.deltaTime);
    }

    void TickSimulation(float deltaTime)
    {
        SimulationDebugController debugCtrl = debugController;
        bool hasController = debugCtrl != null;
        if (hasController)
            debugCtrl.ProcessInput();

        Rect playfield = simulationZone != null ? simulationZone.rect : default;
        _player.Update(deltaTime, playfield);

        SpellModifications mods = castModifications != null
            ? castModifications.GetModifications()
            : new SpellModifications();
        EvaluateItems(mods);

        var sim = _simulation.State;
        bool castRequested = Input.GetKeyDown(castInputKey);
        SpellCastResult castResult = _loopedSpellCaster.AttemptToCastNextSpell(
            sim.SimulationTime, _player.Position, castRequested, mods);
        _presentation.PlayCastAudio(castResult, _loopedSpellCaster.Spells, _player.Position);
        _loopedSpellCaster.Update(sim.SimulationTime, CastForward);

        bool advanceTime = !hasController || debugCtrl.ShouldAdvanceTime;
        if (advanceTime)
        {
            float dt = hasController ? debugCtrl.DeltaTime : deltaTime;
            _simulation.AdvanceTime(dt);
        }

        Stopwatch sw = debugLogTiming ? new Stopwatch() : null;

        CombatReactionContractBuilder.Build(
            _runtimeConfig.playerInventory,
            mods,
            _loopedSpellCaster.Spells,
            Allocator.TempJob,
            out NativeArray<CombatSpawnContract> combatContracts);
        try
        {
            _simulation.SetFrameCombatReactionContracts(combatContracts);

            for (int i = 0; i < _simulation.StepCount; i++)
            {
                if (!hasController || debugCtrl.ShouldRunPhase(i, _simulation.GetStepName(i)))
                {
                    sw?.Restart();
                    _simulation.ExecuteStep(i);
                    if (sw != null)
                    {
                        long ms = sw.ElapsedMilliseconds;
                        if (debugLogTiming)
                            Debug.Log($"[LabbingScene] {_simulation.GetStepName(i)}: {ms}ms");
                    }
                }
            }
        }
        finally
        {
            _simulation.ClearFrameCombatReactionContracts();
            if (combatContracts.IsCreated)
                combatContracts.Dispose();
        }

        float frameDt = hasController ? debugCtrl.DeltaTime : deltaTime;
        _telemetryAggregator.ProcessFrame(sim, frameDt, castResult);

        _presentation.ConsumeFrame(sim);
        _simulation.ClearFrameCombatEvents();

        if (advanceTime)
        {
            float effectDt = hasController ? debugCtrl.DeltaTime : deltaTime;
            _presentation.Update(effectDt);
        }

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        _presentation.Render(sim, simulationZone, cam);

        if (hasController)
            debugCtrl.NotifyFrameComplete();
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

    void EvaluateItems(SpellModifications mods)
    {
        _effectContext.frameMetrics = _telemetryAggregator.CurrentFrame.aggregate;
        _effectContext.spellCastMetrics = _telemetryAggregator.CurrentSpellCast.aggregate;
        _effectContext.spellLoopMetrics = _telemetryAggregator.CurrentSpellLoop.aggregate;
        _effectContext.roundMetrics = _telemetryAggregator.CurrentRound.aggregate;
        _effectContext.gameMetrics = _telemetryAggregator.Game.aggregate;
        _effectContext.spellModifications = mods;

        _effectContext.spellInvocation = new SpellInvocationContext
        {
            totalSpellsCasted = _loopedSpellCaster.TotalInvocationCount,
            spellLoopNumber = _loopedSpellCaster.LoopCount + 1,
            spellSlotNumber = _loopedSpellCaster.NextCastIndex + 1,
            spellLoopSlotCount = _loopedSpellCaster.SpellCount,
            spellLoopsPerRound = int.MaxValue,
            spells = _loopedSpellCaster.Spells,
        };

        _lastItemResults.Clear();
        var items = _runtimeConfig.playerInventory.GetPassiveItems();
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null) continue;
            bool applied = item.Apply(_effectContext);
            _lastItemResults.Add(new ItemEvalResult { itemName = item.name, applied = applied });
            if (debugLogItemEval && applied)
                Debug.Log($"[LabbingScene] Item applied: {item.name}");
        }
    }

    void OnDestroy()
    {
        _presentation?.Dispose();
        _emissionTargetProvider?.Dispose();
        _simulation?.Dispose();
        _runtimeConfig = null;
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null)
            return;

        Transform zone = simulationZone.transform;
        brushController?.DrawGizmos(zone);
        _presentation?.DrawGizmos(zone);

        if (_simulation == null)
            return;

        var drawables = _simulation.GetDebugDrawables();
        for (int i = 0; i < drawables.Count; i++)
            drawables[i].DrawGizmos(zone);
    }
}
