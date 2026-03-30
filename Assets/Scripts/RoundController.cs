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
/// Phases within a single round. Managed by <see cref="RoundController"/>.
/// </summary>
public enum GameLoopPhase
{
    Playing,
    AwaitingDespawn,
    RoundEnd,
    Shop,
    Lose
}

/// <summary>
/// Result of one frame of round simulation. The runner uses this to decide transitions.
/// </summary>
public struct RoundTickResult
{
    public bool roundEnded;
    public bool quotaMet;
}

/// <summary>
/// Configuration passed to <see cref="RoundController"/> so it doesn't reference MonoBehaviour fields directly.
/// </summary>
public class RoundControllerConfig
{
    public KeyCode castInputKey;
    public bool debugLogTiming;
    /// <summary>Session clone from <see cref="GameConfig.CreateRuntimeCopy"/>; use <see cref="GameConfig.playerInventory"/> for items.</summary>
    public GameConfig gameConfig;
    public SpellModificationsTestData castModifications;
    public SimulationDebugController debugController;
}

/// <summary>
/// Owns round-level state (phase, round number, quota, blood tracking) and runs one frame of
/// round gameplay: player movement, casting, simulation steps, telemetry, damage/effect spawning,
/// rendering, and phase evaluation. Reports whether the round ended.
/// Does not own the systems it operates on; the runner creates them and passes them in.
/// </summary>
public class RoundController
{
    private readonly Player _player;
    private readonly GameSimulation _simulation;
    private readonly LoopedSpellCaster _loopedSpellCaster;
    private readonly TelemetryAggregator _telemetryAggregator;
    private readonly DamageNumberController _damageNumberController;
    private readonly EffectSpriteController _effectSpriteController;
    private readonly SpriteInstanceBuilder _spriteInstanceBuilder;
    private readonly SpriteInstancedRenderer _spriteRenderer;
    private readonly AttackEntityDebugRenderer _attackDebugRenderer;
    private readonly RoundControllerConfig _config;
    private readonly EffectContext _effectContext = new EffectContext();
    private readonly List<ItemEvalResult> _lastItemResults = new List<ItemEvalResult>();

    public GameLoopPhase Phase { get; private set; }
    public int RoundNumber { get; private set; }
    public float BloodQuota { get; private set; }
    public int SpellLoopsPerRound { get; private set; }
    public float BloodExtractedThisRound { get; private set; }
    public bool QuotaMet { get; private set; }

    public IReadOnlyList<ItemEvalResult> LastItemResults => _lastItemResults;

    public int SpellLoopSlotCount => _loopedSpellCaster.SpellCount;

    public int IndexOfLastCastInLoop => _loopedSpellCaster.IndexOfLastCast;

    public int LoopsCompletedThisRound => _loopedSpellCaster.LoopCount;

    public RoundController(
        Player player,
        GameSimulation simulation,
        LoopedSpellCaster loopedSpellCaster,
        TelemetryAggregator telemetryAggregator,
        DamageNumberController damageNumberController,
        EffectSpriteController effectSpriteController,
        SpriteInstanceBuilder spriteInstanceBuilder,
        SpriteInstancedRenderer spriteRenderer,
        AttackEntityDebugRenderer attackDebugRenderer,
        RoundControllerConfig config)
    {
        _player = player;
        _simulation = simulation;
        _loopedSpellCaster = loopedSpellCaster;
        _telemetryAggregator = telemetryAggregator;
        _damageNumberController = damageNumberController;
        _effectSpriteController = effectSpriteController;
        _spriteInstanceBuilder = spriteInstanceBuilder;
        _spriteRenderer = spriteRenderer;
        _attackDebugRenderer = attackDebugRenderer;
        _config = config;

        RoundNumber = 1;
        Phase = GameLoopPhase.Playing;
        ApplyRoundRuntimeFromConfig();
    }

    /// <summary>
    /// Swap the active runtime config (e.g. Lose → Retry builds a new <see cref="GameConfig.CreateRuntimeCopy"/>).
    /// </summary>
    public void SetGameConfig(GameConfig runtime)
    {
        _config.gameConfig = runtime;
    }

    /// <summary>
    /// Runs one frame of the round. Returns whether the round ended this frame and the quota result.
    /// </summary>
    public RoundTickResult Tick(float deltaTime, Rect rect, Camera cam, RectTransform simulationZone)
    {
        var debugCtrl = _config.debugController;
        bool hasController = debugCtrl != null;
        if (hasController)
            debugCtrl.ProcessInput();

        _player.Update(deltaTime, rect);

        bool loopsExhausted = _loopedSpellCaster.LoopCount >= SpellLoopsPerRound;
        bool allowCasting = Phase == GameLoopPhase.Playing && !loopsExhausted;
        bool castRequested = allowCasting && Input.GetKeyDown(_config.castInputKey);
        var mods = _config.castModifications != null
            ? _config.castModifications.GetModifications()
            : new SpellModifications();

        EvaluateItems(mods);

        SpellCastResult castResult = _loopedSpellCaster.AttemptToCastNextSpell(
            _simulation.SimulationTime, _player.Position, castRequested, mods);
        _loopedSpellCaster.Update(_simulation.SimulationTime, new float2(-1f, 0f));

        bool advanceTime = !hasController || debugCtrl.ShouldAdvanceTime;
        if (advanceTime)
        {
            float dt = hasController ? debugCtrl.DeltaTime : deltaTime;
            _simulation.AdvanceTime(dt);
        }

        Stopwatch sw = _config.debugLogTiming ? new Stopwatch() : null;
        long totalMs = 0;

        for (int i = 0; i < _simulation.StepCount; i++)
        {
            if (!hasController || debugCtrl.ShouldRunPhase(i, _simulation.GetStepName(i)))
            {
                sw?.Restart();
                _simulation.ExecuteStep(i);
                if (sw != null)
                {
                    long ms = sw.ElapsedMilliseconds;
                    totalMs += ms;
                    if (_config.debugLogTiming)
                        Debug.Log($"[Timing] {_simulation.GetStepName(i)}: {ms}ms");
                }
            }
        }

        float frameDt = hasController ? debugCtrl.DeltaTime : deltaTime;
        _telemetryAggregator.ProcessFrame(
            _simulation.GetDamageEvents(),
            _simulation.GetStatusAilmentAppliedEvents(),
            frameDt,
            _simulation.SimulationTime,
            castResult);

        BloodExtractedThisRound = _telemetryAggregator.CurrentRound.aggregate.bloodExtracted;

        _damageNumberController.SpawnFromDamageEvents(_simulation.GetDamageEvents(), _simulation.GetEnemies());
        _effectSpriteController.SpawnFromDamageEvents(_simulation.GetDamageEvents(), _simulation.GetAttackEntities());
        _simulation.ClearDamageEvents();
        _simulation.ClearStatusAilmentAppliedEvents();

        if (advanceTime)
        {
            float effectDt = hasController ? debugCtrl.DeltaTime : deltaTime;
            _damageNumberController.Update(effectDt);
            _effectSpriteController.Update(effectDt);
        }

        _spriteInstanceBuilder.Build(_simulation.GetEnemies(), _simulation.GetAttackEntities(), _effectSpriteController.GetEntities());
        _spriteRenderer.Render(_spriteInstanceBuilder.Buffer, _spriteInstanceBuilder.Count, simulationZone, cam);
        _attackDebugRenderer.Render(_simulation.GetAttackEntities(), simulationZone, cam);
        _damageNumberController.Render(simulationZone, cam);

        if (hasController)
            debugCtrl.NotifyFrameComplete();

        if (_config.debugLogTiming)
            Debug.Log($"[Timing] Total: {totalMs}ms");

        UpdatePhase(
            loopsExhausted,
            _loopedSpellCaster.HasActiveCasts,
            _loopedSpellCaster.HasPendingSpawns,
            _simulation.GetAttackEntityManager().EntityCount);

        if (Phase == GameLoopPhase.RoundEnd)
        {
            _telemetryAggregator.EndRound();
            EvaluateRoundEnd();
            return new RoundTickResult { roundEnded = true, quotaMet = QuotaMet };
        }

        return default;
    }

    /// <summary>
    /// Advances to the next round. Call from Shop → Round transition.
    /// </summary>
    public void StartNextRound()
    {
        RoundNumber++;
        BloodExtractedThisRound = 0f;
        QuotaMet = false;
        Phase = GameLoopPhase.Playing;
        ApplyRoundRuntimeFromConfig();
        Debug.Log($"[RoundController] Starting round {RoundNumber}. Quota: {BloodQuota:F0}, Loops: {SpellLoopsPerRound}");
    }

    /// <summary>
    /// Resets to round 1. Call from Lose → Round transition.
    /// </summary>
    public void Retry()
    {
        RoundNumber = 1;
        BloodExtractedThisRound = 0f;
        QuotaMet = false;
        Phase = GameLoopPhase.Playing;
        ApplyRoundRuntimeFromConfig();
        Debug.Log($"[RoundController] Retrying from round 1. Quota: {BloodQuota:F0}, Loops: {SpellLoopsPerRound}");
    }

    void ApplyRoundRuntimeFromConfig()
    {
        GameConfig gc = _config.gameConfig;
        BloodQuota = gc.bloodQuotaScaling.BuildForRound(RoundNumber).bloodRequirement;
        SpellLoopsPerRound = Mathf.Max(0, gc.maxSpellLoopsPerRound);
    }

    bool UpdatePhase(bool loopsExhausted, bool hasActiveCasts, bool hasPendingSpawns, int attackEntityCount)
    {
        GameLoopPhase before = Phase;

        switch (Phase)
        {
            case GameLoopPhase.Playing:
                if (loopsExhausted)
                    Phase = GameLoopPhase.AwaitingDespawn;
                break;

            case GameLoopPhase.AwaitingDespawn:
                if (!hasActiveCasts && !hasPendingSpawns && attackEntityCount == 0)
                    Phase = GameLoopPhase.RoundEnd;
                break;
        }

        return Phase != before;
    }

    void EvaluateRoundEnd()
    {
        QuotaMet = BloodExtractedThisRound >= BloodQuota;
        Phase = QuotaMet ? GameLoopPhase.Shop : GameLoopPhase.Lose;
        Debug.Log($"[RoundController] Round {RoundNumber} ended. Blood: {BloodExtractedThisRound:F0} / {BloodQuota:F0} — {(QuotaMet ? "QUOTA MET" : "QUOTA FAILED")}");
    }

    void EvaluateItems(SpellModifications mods)
    {
        _effectContext.frameMetrics = _telemetryAggregator.CurrentFrame.aggregate;
        _effectContext.spellCastMetrics = _telemetryAggregator.CurrentSpellCast.aggregate;
        _effectContext.spellLoopMetrics = _telemetryAggregator.CurrentSpellLoop.aggregate;
        _effectContext.roundMetrics = _telemetryAggregator.CurrentRound.aggregate;
        _effectContext.gameMetrics = _telemetryAggregator.Game.aggregate;
        _effectContext.spellModifications = mods;

        _lastItemResults.Clear();
        var items = _config.gameConfig.playerInventory.GetPassiveItems();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            _lastItemResults.Add(new ItemEvalResult
            {
                itemName = item.name,
                applied = item.Apply(_effectContext)
            });
        }
    }
}
