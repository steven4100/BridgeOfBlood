using System.Collections.Generic;
using System.Diagnostics;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Configuration for <see cref="CombatSimulationController"/> so callers do not pass MonoBehaviour fields ad hoc.
/// </summary>
public sealed class CombatSimulationControllerConfig
{
    public GameConfig RuntimeGameConfig;
    public float PlayerMoveSpeed;
    public SpellModificationsTestData CastModifications;
    public bool DebugLogTiming;
    public bool DebugLogItemEval;
    public SimulationDebugController DebugController;
}

/// <summary>
/// Owns combat simulation coordination: item evaluation, player, casting, time advance, simulation steps,
/// and frame-complete events. Presentation is owned by <see cref="CombatPresentationDriver"/> via the event bus.
/// Callers own cast-request gating and pass <c>spellLoopsPerRound</c> into <see cref="Tick"/>.
/// </summary>
public sealed class CombatSimulationController
{
    static readonly float2 CastForward = new float2(-1f, 0f);

    readonly CombatSimulationControllerConfig _config;
    readonly Player _player;
    readonly GameSimulation _simulation;
    readonly LoopedSpellCaster _loopedSpellCaster;
    readonly SpellEmissionHandler _emissionHandler;
    readonly TelemetryAggregator _telemetryAggregator;
    readonly EnemyEmissionTargetProvider _emissionTargetProvider;
    readonly EffectContext _effectContext = new EffectContext();
    readonly List<ItemEvalResult> _lastItemResults = new List<ItemEvalResult>();

    public GameConfig RuntimeGameConfig => _config.RuntimeGameConfig;
    public GameSimulation Simulation => _simulation;
    public TelemetryAggregator TelemetryAggregator => _telemetryAggregator;
    public LoopedSpellCaster LoopedSpellCaster => _loopedSpellCaster;
    public IReadOnlyList<ItemEvalResult> LastItemResults => _lastItemResults;

    public CombatSimulationController(CombatSimulationControllerConfig config)
    {
        _config = config;
        GameConfig runtime = config.RuntimeGameConfig;
        PlayerInventory inventory = runtime.playerInventory;

        _simulation = new GameSimulation(runtime.simulationConfig);

        Rect playfield = _simulation.State.Playfield;
        _player = new Player(
            new float2(playfield.xMax - 10f, playfield.center.y),
            config.PlayerMoveSpeed);

        _emissionTargetProvider = new EnemyEmissionTargetProvider(_simulation.EnemyManager);
        _emissionHandler = new SpellEmissionHandler(_simulation.AttackEntityManager, _emissionTargetProvider);
        _loopedSpellCaster = new LoopedSpellCaster(inventory.SpellCollection, _emissionHandler);

        int initialSpellCount = Mathf.Max(8, inventory.SpellCollection.Count);
        _telemetryAggregator = new TelemetryAggregator(initialSpellCount);

        if (config.DebugController != null)
            config.DebugController.Initialize(_simulation.StepCount);
    }

    /// <summary>
    /// Swap the active runtime config (e.g. Lose → Retry builds a new <see cref="GameConfig.CreateRuntimeCopy"/>).
    /// </summary>
    public void SetRuntimeGameConfig(GameConfig runtime)
    {
        _config.RuntimeGameConfig = runtime;
    }

    /// <summary>
    /// Clears simulation/cast state and repositions the player for a fresh round.
    /// </summary>
    public void ResetForNewRound()
    {
        _simulation.ResetForNewRound();
        _loopedSpellCaster.Reset();
        _loopedSpellCaster.ClearCastState();
        _player.PlaceAtRightSide(_simulation.State.Playfield);
    }

    /// <summary>
    /// One combat frame: evaluate items into frame mods, then cast / step / raise frame-complete.
    /// </summary>
    public void Tick(float deltaTime, bool castRequested, int spellLoopsPerRound)
    {
        SpellModifications mods = _config.CastModifications != null
            ? _config.CastModifications.GetModifications()
            : new SpellModifications();
        EvaluateItems(mods, spellLoopsPerRound);

        SimulationDebugController debugCtrl = _config.DebugController;
        bool hasController = debugCtrl != null;
        if (hasController)
            debugCtrl.ProcessInput();

        Rect playfield = _simulation.State.Playfield;
        _player.Update(deltaTime, playfield);

        _emissionHandler.SetFrameModifications(mods);

        var sim = _simulation.State;
        SpellCastResult castResult = _loopedSpellCaster.AttemptToCastNextSpell(
            sim.SimulationTime, _player.Position, castRequested);
        if (castResult.didCast)
        {
            SharedGameEventBus.Bus.Raise(new SpellCastEvent
            {
                castResult = castResult,
                spells = _loopedSpellCaster.Spells,
                origin = _player.Position
            });
        }
        _loopedSpellCaster.Update(sim.SimulationTime, CastForward);

        bool advanceTime = !hasController || debugCtrl.ShouldAdvanceTime;
        if (advanceTime)
        {
            float dt = hasController ? debugCtrl.DeltaTime : deltaTime;
            _simulation.AdvanceTime(dt);
        }

        Stopwatch sw = _config.DebugLogTiming ? new Stopwatch() : null;

        CombatReactionContractBuilder.Build(
            _config.RuntimeGameConfig.playerInventory,
            mods,
            _loopedSpellCaster.Spells,
            out List<CombatSpawnContract> combatContracts);
        try
        {
            _simulation.SetFrameCombatReactionContracts(combatContracts);

            for (int i = 0; i < _simulation.StepCount; i++)
            {
                if (!hasController || debugCtrl.ShouldRunPhase(i, _simulation.GetStepName(i)))
                {
                    sw?.Restart();
                    _simulation.ExecuteStep(i);
                    if (sw != null && _config.DebugLogTiming)
                        Debug.Log($"[CombatSimulationController] {_simulation.GetStepName(i)}: {sw.ElapsedMilliseconds}ms");
                }
            }
        }
        finally
        {
            _simulation.ClearFrameCombatReactionContracts();
        }

        float frameDt = hasController ? debugCtrl.DeltaTime : deltaTime;
        SharedGameEventBus.Bus.Raise(new SimulationCompleteEvent
        {
            simulationState = sim,
            deltaTime = frameDt,
            simulationTime = sim.SimulationTime,
            simulationAdvanced = advanceTime,
            spellCastResult = castResult,
            playerPosition = _player.Position
        });

        _simulation.ClearFrameCombatEvents();

        if (hasController)
            debugCtrl.NotifyFrameComplete();
    }

    void EvaluateItems(SpellModifications mods, int spellLoopsPerRound)
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
            spellLoopsPerRound = spellLoopsPerRound,
            spells = _loopedSpellCaster.Spells,
        };

        _lastItemResults.Clear();
        var items = _config.RuntimeGameConfig.playerInventory.GetPassiveItems();
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null) continue;
            bool applied = item.Apply(_effectContext);
            _lastItemResults.Add(new ItemEvalResult { itemName = item.name, applied = applied });
            if (_config.DebugLogItemEval && applied)
                Debug.Log($"[CombatSimulationController] Item applied: {item.name}");
        }
    }

    public void DrawGizmos(Transform zone)
    {
        if (_simulation == null)
            return;

        var drawables = _simulation.GetDebugDrawables();
        for (int i = 0; i < drawables.Count; i++)
            drawables[i].DrawGizmos(zone);
    }

    public void Dispose()
    {
        _telemetryAggregator?.Dispose();
        _emissionTargetProvider?.Dispose();
        _simulation?.Dispose();
    }
}
