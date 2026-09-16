using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using BridgeOfBlood.Telemetry;
using Unity.Collections;

/// <summary>
/// Aggregates subscribed telemetry stats at nested time scales: Frame, Spell Loop, Round, Game.
/// Only authored (plus always-on core) queries are materialized.
/// </summary>
public class TelemetryAggregator : ITelemetryStatLookup
{
    struct Subscription
    {
        public TelemetryStatArgs args;
        public string key;
        public ITelemetryStat<float> stat;
        public bool isAdditive;
        public bool foldPerSpell;
    }

    sealed class SparseStore
    {
        public readonly Dictionary<string, float> Aggregate;
        public readonly Dictionary<int, Dictionary<string, float>> PerSpell;

        public SparseStore(int spellCapacity)
        {
            Aggregate = new Dictionary<string, float>(32);
            PerSpell = new Dictionary<int, Dictionary<string, float>>(spellCapacity);
        }

        public void Reset()
        {
            Aggregate.Clear();
            PerSpell.Clear();
        }

        public void Add(string key, float value)
        {
            Aggregate.TryGetValue(key, out float existing);
            Aggregate[key] = existing + value;
        }

        public void AddForSpell(int spellId, string key, float value)
        {
            if (!PerSpell.TryGetValue(spellId, out Dictionary<string, float> dict))
            {
                dict = new Dictionary<string, float>(8);
                PerSpell[spellId] = dict;
            }

            dict.TryGetValue(key, out float existing);
            dict[key] = existing + value;
        }

        public float Get(string key)
        {
            Aggregate.TryGetValue(key, out float value);
            return value;
        }

        public float GetForSpell(int spellId, string key)
        {
            if (PerSpell.TryGetValue(spellId, out Dictionary<string, float> dict)
                && dict.TryGetValue(key, out float value))
                return value;
            return 0f;
        }
    }

    readonly TelemetryStatRegistry<float> _registry;
    readonly List<Subscription> _subscriptions;
    readonly HashSet<string> _subscriptionKeys;
    readonly HashSet<int> _frameSpellIds;

    readonly SparseStore _frame;
    readonly SparseStore _loop;
    readonly SparseStore _round;
    readonly SparseStore _game;

    FrameSnapshot _currentFrame;
    int _currentSpellCastId;
    int _currentSpellCastInvocation;
    int _currentLoopIndex;
    int _currentRoundNumber;
    int _roundLoopsCompleted;
    int _gameRoundsCompleted;

    readonly Dictionary<int, int> _spellLoopInvocations;
    readonly Dictionary<int, int> _roundInvocations;
    readonly Dictionary<int, int> _gameInvocations;

    SpellTelemetrySnapshot[] _perSpellBuffer;

    public FrameSnapshot CurrentFrame => _currentFrame;

    public SpellCastSnapshot CurrentSpellCast => new SpellCastSnapshot
    {
        spellId = _currentSpellCastId,
        invocationId = _currentSpellCastInvocation
    };

    public SpellLoopSnapshot CurrentSpellLoop => new SpellLoopSnapshot
    {
        loopIndex = _currentLoopIndex,
        perSpell = BuildPerSpellArray(_loop, _spellLoopInvocations)
    };

    public RoundSnapshot CurrentRound => new RoundSnapshot
    {
        roundNumber = _currentRoundNumber,
        loopsCompleted = _roundLoopsCompleted,
        perSpell = BuildPerSpellArray(_round, _roundInvocations)
    };

    public GameSnapshot Game => new GameSnapshot
    {
        roundsCompleted = _gameRoundsCompleted,
        perSpell = BuildPerSpellArray(_game, _gameInvocations)
    };

    public TelemetryAggregator(int spellCount)
    {
        int cap = spellCount > 0 ? spellCount : 4;
        _registry = TelemetryStatRegistryFactory.Create();
        _subscriptions = new List<Subscription>(32);
        _subscriptionKeys = new HashSet<string>();
        _frameSpellIds = new HashSet<int>();

        _frame = new SparseStore(cap);
        _loop = new SparseStore(cap);
        _round = new SparseStore(cap);
        _game = new SparseStore(cap);

        _spellLoopInvocations = new Dictionary<int, int>(cap);
        _roundInvocations = new Dictionary<int, int>(cap);
        _gameInvocations = new Dictionary<int, int>(cap);
        _perSpellBuffer = new SpellTelemetrySnapshot[cap];

        SetSubscriptions(null);
        SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
    }

    public void Dispose()
    {
        SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
    }

    /// <summary>
    /// Replaces extra subscriptions. Always-on core queries are unioned in.
    /// Pass null or empty to subscribe only the core set.
    /// </summary>
    public void SetSubscriptions(IReadOnlyList<TelemetryStatArgs> extra)
    {
        _subscriptions.Clear();
        _subscriptionKeys.Clear();

        TelemetryStatArgs[] core = TelemetryCoreStats.Additive;
        for (int i = 0; i < core.Length; i++)
            TryAddSubscription(core[i]);

        if (extra != null)
        {
            for (int i = 0; i < extra.Count; i++)
                TryAddSubscription(extra[i]);
        }
    }

    void TryAddSubscription(TelemetryStatArgs args)
    {
        if (args == null)
            return;

        string key = args.GetKey();
        if (!_subscriptionKeys.Add(key))
            return;

        ITelemetryStat<float> stat = _registry.Resolve(args);
        _subscriptions.Add(new Subscription
        {
            args = args,
            key = key,
            stat = stat,
            isAdditive = stat.IsAdditive,
            foldPerSpell = stat.FoldPerSpell
        });
    }

    void OnSimulationComplete(ref SimulationCompleteEvent @event)
    {
        ProcessFrame(
            @event.simulationState,
            @event.deltaTime,
            @event.spellCastResult);
    }

    public void ProcessFrame(
        GameSimulation.SimulationState state,
        float deltaTime,
        SpellCastResult castResult)
    {
        if (castResult.didCast || castResult.loopCompleted)
            OnSpellCast(castResult);

        IngestFrame(state, deltaTime);
        AccumulateFrameIntoHigherLevels();

        _currentFrame = new FrameSnapshot
        {
            deltaTime = deltaTime,
            simulationTime = state.SimulationTime
        };
    }

    void IngestFrame(GameSimulation.SimulationState state, float deltaTime)
    {
        _frame.Reset();
        _frameSpellIds.Clear();

        var context = new TelemetryStatEvalContext(
            state.DamageEvents,
            state.TickDamageEvents,
            state.StatusAilmentAppliedEvents,
            state.AttackEntityRemovalEvents,
            deltaTime);

        for (int i = 0; i < _subscriptions.Count; i++)
        {
            Subscription sub = _subscriptions[i];
            if (!sub.isAdditive)
                continue;
            float value = sub.stat.ProcessEvents(sub.args, in context);
            _frame.Add(sub.key, value);
        }

        CollectSpellIds(state.DamageEvents);
        CollectSpellIds(state.TickDamageEvents);
        CollectSpellIds(state.StatusAilmentAppliedEvents);

        foreach (int spellId in _frameSpellIds)
        {
            var spellContext = new TelemetryStatEvalContext(
                state.DamageEvents,
                state.TickDamageEvents,
                state.StatusAilmentAppliedEvents,
                state.AttackEntityRemovalEvents,
                deltaTime,
                spellId,
                hasSpellFilter: true);

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                Subscription sub = _subscriptions[i];
                if (!sub.isAdditive || !sub.foldPerSpell)
                    continue;
                float value = sub.stat.ProcessEvents(sub.args, in spellContext);
                _frame.AddForSpell(spellId, sub.key, value);
            }
        }
    }

    void CollectSpellIds(NativeArray<DamageEvent> events)
    {
        for (int i = 0; i < events.Length; i++)
            _frameSpellIds.Add(events[i].spellId);
    }

    void CollectSpellIds(NativeArray<TickDamageEvent> events)
    {
        for (int i = 0; i < events.Length; i++)
            _frameSpellIds.Add(events[i].spellId);
    }

    void CollectSpellIds(NativeArray<StatusAilmentAppliedEvent> events)
    {
        for (int i = 0; i < events.Length; i++)
            _frameSpellIds.Add(events[i].spellId);
    }

    void AccumulateFrameIntoHigherLevels()
    {
        MergeStore(_loop, _frame);
        MergeStore(_round, _frame);
        MergeStore(_game, _frame);
    }

    static void MergeStore(SparseStore target, SparseStore source)
    {
        foreach (var kvp in source.Aggregate)
            target.Add(kvp.Key, kvp.Value);

        foreach (var spell in source.PerSpell)
        {
            int spellId = spell.Key;
            foreach (var kvp in spell.Value)
                target.AddForSpell(spellId, kvp.Key, kvp.Value);
        }
    }

    void OnSpellCast(SpellCastResult castResult)
    {
        if (castResult.loopCompleted)
        {
            _roundLoopsCompleted++;
            _currentLoopIndex = castResult.loopCount;
            _loop.Reset();
            _spellLoopInvocations.Clear();
        }

        if (!castResult.didCast)
            return;

        TrackInvocation(_spellLoopInvocations, castResult.spellId);
        TrackInvocation(_roundInvocations, castResult.spellId);
        TrackInvocation(_gameInvocations, castResult.spellId);

        _currentSpellCastId = castResult.spellId;
        _currentSpellCastInvocation = castResult.invocationCount;
    }

    public void EndRound()
    {
        _gameRoundsCompleted++;
        _currentRoundNumber++;
        _round.Reset();
        _roundInvocations.Clear();
        _roundLoopsCompleted = 0;
    }

    public float GetTelemetry(MetricsScope scope, string key)
    {
        return Store(scope).Get(key);
    }

    public float GetTelemetryForSpell(MetricsScope scope, string key, int spellId)
    {
        return Store(scope).GetForSpell(spellId, key);
    }

    public void FillSubscriptionValues(MetricsScope scope, List<KeyValuePair<string, float>> destination)
    {
        destination.Clear();
        SparseStore store = Store(scope);
        for (int i = 0; i < _subscriptions.Count; i++)
        {
            Subscription sub = _subscriptions[i];
            if (!sub.isAdditive)
                continue;
            destination.Add(new KeyValuePair<string, float>(sub.key, store.Get(sub.key)));
        }

        float duration = store.Get(TelemetryCoreStats.DurationKey);
        float damage = store.Get(TelemetryCoreStats.TotalDamageKey);
        destination.Add(new KeyValuePair<string, float>(
            TelemetryCoreStats.DpsKey,
            duration > 0f ? damage / duration : 0f));
    }

    public void FillSubscriptionValuesForSpell(
        MetricsScope scope,
        int spellId,
        List<KeyValuePair<string, float>> destination)
    {
        destination.Clear();
        SparseStore store = Store(scope);
        for (int i = 0; i < _subscriptions.Count; i++)
        {
            Subscription sub = _subscriptions[i];
            if (!sub.isAdditive || !sub.foldPerSpell)
                continue;
            destination.Add(new KeyValuePair<string, float>(
                sub.key,
                store.GetForSpell(spellId, sub.key)));
        }
    }

    SparseStore Store(MetricsScope scope) => scope switch
    {
        MetricsScope.Frame => _frame,
        MetricsScope.SpellCast => _loop,
        MetricsScope.SpellLoop => _loop,
        MetricsScope.Round => _round,
        MetricsScope.Game => _game,
        _ => throw new System.ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    static void TrackInvocation(Dictionary<int, int> dict, int spellId)
    {
        dict.TryGetValue(spellId, out int count);
        dict[spellId] = count + 1;
    }

    SpellTelemetrySnapshot[] BuildPerSpellArray(SparseStore store, Dictionary<int, int> invocations)
    {
        int count = store.PerSpell.Count;
        if (count == 0)
            return System.Array.Empty<SpellTelemetrySnapshot>();

        if (_perSpellBuffer.Length < count)
            _perSpellBuffer = new SpellTelemetrySnapshot[count];

        int idx = 0;
        foreach (var kvp in store.PerSpell)
        {
            int invCount = 0;
            invocations?.TryGetValue(kvp.Key, out invCount);

            _perSpellBuffer[idx++] = new SpellTelemetrySnapshot
            {
                spellId = kvp.Key,
                invocationCount = invCount
            };
        }

        var result = new SpellTelemetrySnapshot[count];
        System.Array.Copy(_perSpellBuffer, result, count);
        return result;
    }
}
