using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

/// <summary>
/// Aggregates combat telemetry at five nested time scales: Frame, Spell Cast, Spell Loop, Round, Game.
/// Consumes enriched <see cref="DamageEvent"/> and <see cref="TickDamageEvent"/> lists each frame as sources of truth for hits and DoT ticks.
/// UI and other downstream systems read snapshots via properties -- never hook into combat logic directly.
/// </summary>
public class TelemetryAggregator
{
    private FrameSnapshot _currentFrame;
    private CombatMetrics _currentSpellCastAggregate;
    private CombatMetrics _currentSpellLoopAggregate;
    private CombatMetrics _currentRoundAggregate;
    private CombatMetrics _gameAggregate;

    private int _currentSpellCastId;
    private int _currentSpellCastInvocation;
    private int _currentLoopIndex;
    private int _currentRoundNumber;
    private int _roundLoopsCompleted;
    private int _gameRoundsCompleted;

    private readonly Dictionary<int, CombatMetrics> _framePerSpell;
    private readonly Dictionary<int, CombatMetrics> _spellLoopPerSpell;
    private readonly Dictionary<int, CombatMetrics> _roundPerSpell;
    private readonly Dictionary<int, CombatMetrics> _gamePerSpell;

    private readonly Dictionary<int, int> _spellLoopInvocations;
    private readonly Dictionary<int, int> _roundInvocations;
    private readonly Dictionary<int, int> _gameInvocations;

    private SpellCombatMetrics[] _perSpellBuffer;

    public FrameSnapshot CurrentFrame => _currentFrame;

    public SpellCastSnapshot CurrentSpellCast => new SpellCastSnapshot
    {
        aggregate = _currentSpellCastAggregate,
        spellId = _currentSpellCastId,
        invocationId = _currentSpellCastInvocation
    };

    public SpellLoopSnapshot CurrentSpellLoop => new SpellLoopSnapshot
    {
        aggregate = _currentSpellLoopAggregate,
        loopIndex = _currentLoopIndex,
        perSpell = BuildPerSpellArray(_spellLoopPerSpell, _spellLoopInvocations)
    };

    public RoundSnapshot CurrentRound => new RoundSnapshot
    {
        aggregate = _currentRoundAggregate,
        roundNumber = _currentRoundNumber,
        loopsCompleted = _roundLoopsCompleted,
        perSpell = BuildPerSpellArray(_roundPerSpell, _roundInvocations)
    };

    public GameSnapshot Game => new GameSnapshot
    {
        aggregate = _gameAggregate,
        roundsCompleted = _gameRoundsCompleted,
        perSpell = BuildPerSpellArray(_gamePerSpell, _gameInvocations)
    };

    public TelemetryAggregator(int spellCount)
    {
        _framePerSpell = new Dictionary<int, CombatMetrics>(spellCount);
        _spellLoopPerSpell = new Dictionary<int, CombatMetrics>(spellCount);
        _roundPerSpell = new Dictionary<int, CombatMetrics>(spellCount);
        _gamePerSpell = new Dictionary<int, CombatMetrics>(spellCount);

        _spellLoopInvocations = new Dictionary<int, int>(spellCount);
        _roundInvocations = new Dictionary<int, int>(spellCount);
        _gameInvocations = new Dictionary<int, int>(spellCount);

        _perSpellBuffer = new SpellCombatMetrics[spellCount > 0 ? spellCount : 4];
    }

    /// <summary>
    /// Call once per frame after the damage step. Processes spell cast / loop boundaries first
    /// (so damage from a newly-cast spell starts a fresh window), then iterates the DamageEvent
    /// list to build the frame snapshot and accumulates into all higher-level metrics.
    /// </summary>
    public void ProcessFrame(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<TickDamageEvent> tickDamageEvents,
        NativeArray<StatusAilmentAppliedEvent> statusAilmentEvents,
        float deltaTime,
        float simulationTime,
        SpellCastResult castResult)
    {
        if (castResult.didCast)
            OnSpellCast(castResult);

        BuildFrameSnapshot(damageEvents, tickDamageEvents, statusAilmentEvents, deltaTime, simulationTime);
        AccumulateFrameIntoHigherLevels();
    }

    /// <summary>Feeds LCD combat buffers from <see cref="GameSimulation.State"/> into telemetry.</summary>
    public void ProcessFrame(
        GameSimulation.SimulationState state,
        float deltaTime,
        SpellCastResult castResult)
    {
        ProcessFrame(
            state.DamageEvents,
            state.TickDamageEvents,
            state.StatusAilmentAppliedEvents,
            deltaTime,
            state.SimulationTime,
            castResult);
    }

    private void BuildFrameSnapshot(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<TickDamageEvent> tickDamageEvents,
        NativeArray<StatusAilmentAppliedEvent> statusAilmentEvents,
        float deltaTime,
        float simulationTime)
    {
        _framePerSpell.Clear();
        var metrics = new CombatMetrics { duration = deltaTime };

        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];
            metrics.hits++;
            metrics.totalDamage += evt.damageDealt;
            metrics.physicalDamage += evt.physicalDamage;
            metrics.fireDamage += evt.fireDamage;
            metrics.coldDamage += evt.coldDamage;
            metrics.lightningDamage += evt.lightningDamage;

            metrics.bloodExtracted += evt.bloodExtracted;

            if (evt.isCrit)
                metrics.crits++;
            if (evt.wasKill)
            {
                metrics.kills++;
                metrics.overkillDamage += evt.overkillDamage;
            }

            AccumulateEventIntoDict(_framePerSpell, in evt);
        }

        for (int i = 0; i < tickDamageEvents.Length; i++)
        {
            TickDamageEvent te = tickDamageEvents[i];
            metrics.totalDamage += te.damageDealt;
            metrics.physicalDamage += te.physicalDamage;
            metrics.fireDamage += te.fireDamage;
            metrics.coldDamage += te.coldDamage;
            metrics.lightningDamage += te.lightningDamage;
            metrics.bloodExtracted += te.bloodExtracted;
            if (te.wasKill)
            {
                metrics.kills++;
                metrics.overkillDamage += te.overkillDamage;
            }
            AccumulateTickEventIntoDict(_framePerSpell, in te);
        }

        for (int i = 0; i < statusAilmentEvents.Length; i++)
        {
            StatusAilmentAppliedEvent saEvt = statusAilmentEvents[i];
            AccumulateAilmentEventIntoDict(_framePerSpell, in saEvt);
        }

        _currentFrame = new FrameSnapshot
        {
            aggregate = metrics,
            deltaTime = deltaTime,
            simulationTime = simulationTime
        };
    }

    private void AccumulateFrameIntoHigherLevels()
    {
        ref CombatMetrics frame = ref _currentFrame.aggregate;
        _currentSpellCastAggregate.Accumulate(in frame);
        _currentSpellLoopAggregate.Accumulate(in frame);
        _currentRoundAggregate.Accumulate(in frame);
        _gameAggregate.Accumulate(in frame);

        MergePerSpellFrom(_spellLoopPerSpell, _framePerSpell);
        MergePerSpellFrom(_roundPerSpell, _framePerSpell);
        MergePerSpellFrom(_gamePerSpell, _framePerSpell);
    }

    private void OnSpellCast(SpellCastResult castResult)
    {
        if (castResult.loopCompleted)
        {
            _roundLoopsCompleted++;
            _currentLoopIndex = castResult.loopCount;
            _currentSpellLoopAggregate.Reset();
            _spellLoopPerSpell.Clear();
            _spellLoopInvocations.Clear();
        }

        TrackInvocation(_spellLoopInvocations, castResult.spellId);
        TrackInvocation(_roundInvocations, castResult.spellId);
        TrackInvocation(_gameInvocations, castResult.spellId);

        _currentSpellCastId = castResult.spellId;
        _currentSpellCastInvocation = castResult.invocationCount;
        _currentSpellCastAggregate.Reset();
    }

    /// <summary>
    /// Call when a round ends to snapshot round metrics and reset for the next round.
    /// </summary>
    public void EndRound()
    {
        _gameRoundsCompleted++;
        _currentRoundNumber++;
        _currentRoundAggregate.Reset();
        _roundPerSpell.Clear();
        _roundInvocations.Clear();
        _roundLoopsCompleted = 0;
    }

    private static void AccumulateEventIntoDict(Dictionary<int, CombatMetrics> dict, in DamageEvent evt)
    {
        int key = evt.spellId;
        dict.TryGetValue(key, out CombatMetrics existing);

        existing.hits++;
        existing.totalDamage += evt.damageDealt;
        existing.physicalDamage += evt.physicalDamage;
        existing.fireDamage += evt.fireDamage;
        existing.coldDamage += evt.coldDamage;
        existing.lightningDamage += evt.lightningDamage;
        existing.bloodExtracted += evt.bloodExtracted;
        if (evt.isCrit) existing.crits++;
        if (evt.wasKill)
        {
            existing.kills++;
            existing.overkillDamage += evt.overkillDamage;
        }

        dict[key] = existing;
    }

    private static void AccumulateTickEventIntoDict(Dictionary<int, CombatMetrics> dict, in TickDamageEvent evt)
    {
        int key = evt.spellId;
        dict.TryGetValue(key, out CombatMetrics existing);

        existing.totalDamage += evt.damageDealt;
        existing.physicalDamage += evt.physicalDamage;
        existing.fireDamage += evt.fireDamage;
        existing.coldDamage += evt.coldDamage;
        existing.lightningDamage += evt.lightningDamage;
        existing.bloodExtracted += evt.bloodExtracted;
        if (evt.wasKill)
        {
            existing.kills++;
            existing.overkillDamage += evt.overkillDamage;
        }

        dict[key] = existing;
    }

    private static void AccumulateAilmentEventIntoDict(Dictionary<int, CombatMetrics> dict, in StatusAilmentAppliedEvent evt)
    {
        int key = evt.spellId;
        dict.TryGetValue(key, out CombatMetrics existing);
        AccumulateAilmentIntoMetrics(ref existing, evt.ailmentFlag);
        dict[key] = existing;
    }

    private static void AccumulateAilmentIntoMetrics(ref CombatMetrics metrics, StatusAilmentFlag flag)
    {
        if ((flag & StatusAilmentFlag.Frozen) != 0) metrics.frozenApplied++;
        if ((flag & StatusAilmentFlag.Ignited) != 0) metrics.ignitedApplied++;
        if ((flag & StatusAilmentFlag.Shocked) != 0) metrics.shockedApplied++;
        if ((flag & StatusAilmentFlag.Poisoned) != 0) metrics.poisonedApplied++;
        if ((flag & StatusAilmentFlag.Stunned) != 0) metrics.stunnedApplied++;
        if ((flag & StatusAilmentFlag.Bleeding) != 0) metrics.bleedingApplied++;
    }

    private static void MergePerSpellFrom(Dictionary<int, CombatMetrics> target, Dictionary<int, CombatMetrics> source)
    {
        foreach (var kvp in source)
        {
            target.TryGetValue(kvp.Key, out CombatMetrics existing);
            CombatMetrics sourceMetrics = kvp.Value;
            existing.Accumulate(in sourceMetrics);
            target[kvp.Key] = existing;
        }
    }

    private static void TrackInvocation(Dictionary<int, int> dict, int spellId)
    {
        dict.TryGetValue(spellId, out int count);
        dict[spellId] = count + 1;
    }

    private SpellCombatMetrics[] BuildPerSpellArray(Dictionary<int, CombatMetrics> dict, Dictionary<int, int> invocations)
    {
        if (dict.Count == 0)
            return System.Array.Empty<SpellCombatMetrics>();

        if (_perSpellBuffer.Length < dict.Count)
            _perSpellBuffer = new SpellCombatMetrics[dict.Count];

        int idx = 0;
        foreach (var kvp in dict)
        {
            int invCount = 0;
            invocations?.TryGetValue(kvp.Key, out invCount);

            _perSpellBuffer[idx++] = new SpellCombatMetrics
            {
                spellId = kvp.Key,
                invocationCount = invCount,
                metrics = kvp.Value
            };
        }

        var result = new SpellCombatMetrics[dict.Count];
        System.Array.Copy(_perSpellBuffer, result, dict.Count);
        return result;
    }
}
