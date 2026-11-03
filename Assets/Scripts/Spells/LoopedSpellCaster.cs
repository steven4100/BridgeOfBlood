using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Returned by <see cref="LoopedSpellCaster.AttemptToCastNextSpell"/> each frame.
/// Tells the caller exactly what happened: whether a spell was cast, which one,
/// and whether the spell loop just completed (last slot was cast, or mana reset).
/// </summary>
public struct SpellCastResult
{
    public bool didCast;
    public int spellId;
    public int invocationCount;
    public bool loopCompleted;
    public int loopCount;
    /// <summary>True when this invocation is an automatic multicast recast of the current paid slot.</summary>
    public bool isRecast;

    public static readonly SpellCastResult None = default;
}

/// <summary>
/// One pointer cast command for a simulation frame. Callers own input and pass this into the combat tick.
/// </summary>
public struct SpellCastRequest
{
    public bool requested;
    public float2 origin;
    public float2 direction;
}

/// <summary>
/// Drives a fixed loop of N spells. A slot may automatically invoke multiple times, spaced by its
/// recastCooldown, at most once per frame. The next slot may only be cast after the final invocation's
/// castCompletionDuration.
/// When it's time to cast, emits through its <see cref="SpellEmissionHandler"/>.
/// Plain class — call AttemptToCastNextSpell each frame; call Update each frame to advance pending spawns.
///
/// Also the sole writer of <see cref="RuntimeSpell"/> cast/on-deck state via
/// <see cref="RuntimeSpell.RecordCast"/> and <see cref="UpdateOnDeckFlags"/>.
/// </summary>
public class LoopedSpellCaster
{
    private struct PendingMulticast
    {
        public RuntimeSpell spell;
        public int remainingInvocations;
        public double nextFireTime;
        public double cooldown;
    }

    private readonly SpellCollection _spellCollection;
    private readonly SpellEmissionHandler _emissionHandler;
    private int _indexOfLastCast;
    private double _timeOfLastCast;
    private int _loopCount;
    private readonly List<float> _loopManaCosts = new List<float>();
    private PendingMulticast _pendingMulticast;
    private float2 _pressOrigin;
    private float2 _pressForward;

    public IReadOnlyList<RuntimeSpell> Spells => _spellCollection.RuntimeSpells;

    public int SpellCount => _spellCollection.Count;

    public int IndexOfLastCast => _indexOfLastCast;

    public int LoopCount => _loopCount;

    public int NextCastIndex => _spellCollection.Count > 0
        ? (_indexOfLastCast + 1) % _spellCollection.Count
        : -1;

    /// <summary>
    /// Mana left in the current loop: <paramref name="totalMana"/> minus prefix cost through the last cast slot.
    /// After the last slot is cast, the next loop has not spent yet, so this returns <paramref name="totalMana"/>.
    /// </summary>
    public float GetRemainingMana(float totalMana, ModificationCatalog modifications)
    {
        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        if (spells.Count == 0 || _indexOfLastCast < 0)
            return totalMana;
        if (NextCastIndex == 0 && _indexOfLastCast == spells.Count - 1)
            return totalMana;

        SpellModificationsApplicator.EvaluateLoopManaCosts(spells, modifications, _loopManaCosts);
        float remaining = totalMana - _loopManaCosts[_indexOfLastCast];
        return remaining < 0f ? 0f : remaining;
    }

    public int TotalInvocationCount
    {
        get
        {
            IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
            int total = 0;
            for (int i = 0; i < spells.Count; i++)
                total += spells[i].InvocationCount;
            return total;
        }
    }

    public bool HasActiveCasts => _pendingMulticast.remainingInvocations > 0;

    public bool HasPendingSpawns => _emissionHandler != null && _emissionHandler.HasPendingSpawns;

    public SpellAttributeMask GetSpellAttributeMask(int loopIndex)
    {
        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        if (loopIndex < 0 || loopIndex >= spells.Count)
            return SpellAttributeMask.None;
        var def = spells[loopIndex].Definition;
        return def != null ? def.attributeMask : SpellAttributeMask.None;
    }

    public LoopedSpellCaster(SpellCollection spellCollection, SpellEmissionHandler emissionHandler)
    {
        _spellCollection = spellCollection;
        _emissionHandler = emissionHandler ?? throw new System.ArgumentNullException(nameof(emissionHandler));
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
    }

    /// <summary>
    /// Casts the next spell in the loop if timing and mana allow. Spell modifications are applied at spawn time by
    /// <see cref="AttackEntityManager"/> (fed via <see cref="SpellEmissionHandler"/>), not here.
    /// When the prefix mana cost of the next slot exceeds <paramref name="totalMana"/>, the loop counts as
    /// completed and this attempt casts the first spell of the new loop (if that spell fits the budget).
    /// </summary>
    public SpellCastResult AttemptToCastNextSpell(
        double roundTime,
        float2 origin,
        float2 forward,
        bool castRequestedThisFrame,
        ModificationCatalog modifications,
        float totalMana)
    {
        SpellCastResult recastResult = TryFlushDueMulticast(roundTime);
        if (recastResult.didCast)
            return recastResult;

        if (!castRequestedThisFrame)
            return SpellCastResult.None;

        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        if (spells.Count == 0)
            return SpellCastResult.None;

        int nextIndex = (_indexOfLastCast + 1) % spells.Count;
        bool canCastNext;

        if (_indexOfLastCast < 0)
        {
            canCastNext = true;
        }
        else
        {
            RuntimeSpell last = spells[_indexOfLastCast];
            SpellAuthoringData definition = last.Definition;
            float baseDuration = definition != null ? definition.castCompletionDuration : 0f;
            SpellAttributeMask mask = definition != null
                ? definition.attributeMask
                : SpellAttributeMask.None;
            ModificationSet lastModifications = modifications != null
                ? modifications.ResolveFor(last.spellId)
                : null;
            double requiredElapsed = SpellModificationsApplicator.ResolveCastCompletionDuration(
                lastModifications, baseDuration, mask);
            canCastNext = _pendingMulticast.remainingInvocations == 0
                && (roundTime - _timeOfLastCast) >= requiredElapsed;
        }

        if (!canCastNext)
            return SpellCastResult.None;

        SpellModificationsApplicator.EvaluateLoopManaCosts(spells, modifications, _loopManaCosts);
        bool completedByManaReset = false;
        if (_loopManaCosts[nextIndex] > totalMana)
        {
            _indexOfLastCast = -1;
            _loopCount++;
            if (nextIndex == 0 || _loopManaCosts[0] > totalMana)
            {
                UpdateOnDeckFlags(spells);
                return new SpellCastResult
                {
                    didCast = false,
                    loopCompleted = true,
                    loopCount = _loopCount
                };
            }

            nextIndex = 0;
            completedByManaReset = true;
        }

        bool loopCompleted = completedByManaReset || nextIndex == spells.Count - 1;
        if (loopCompleted && !completedByManaReset)
            _loopCount++;

        RuntimeSpell next = spells[nextIndex];
        if (next.Definition == null)
            return SpellCastResult.None;

        _indexOfLastCast = nextIndex;
        UpdateOnDeckFlags(spells);
        _pressOrigin = origin;
        _pressForward = forward;

        SpellCastResult pressResult = StartInvocation(next, origin, forward, roundTime, loopCompleted, isRecast: false);

        SpellAuthoringData nextDefinition = next.Definition;
        ModificationSet nextModifications = modifications != null
            ? modifications.ResolveFor(next.spellId)
            : null;
        int multicastCount = SpellModificationsApplicator.ResolveMulticastCount(
            nextModifications,
            nextDefinition.attributeMask);
        if (multicastCount > 1)
        {
            double cooldown = System.Math.Max(0.0, nextDefinition.recastCooldown);
            _pendingMulticast = new PendingMulticast
            {
                spell = next,
                remainingInvocations = multicastCount - 1,
                nextFireTime = roundTime + cooldown,
                cooldown = cooldown
            };
        }

        return pressResult;
    }

    SpellCastResult StartInvocation(
        RuntimeSpell spell,
        float2 origin,
        float2 forward,
        double fireTime,
        bool loopCompleted,
        bool isRecast)
    {
        spell.RecordCast(fireTime);
        _timeOfLastCast = fireTime;
        _emissionHandler.Emit(
            spell,
            origin,
            forward,
            (float)fireTime,
            spell.spellId,
            spell.InvocationCount);

        return new SpellCastResult
        {
            didCast = true,
            spellId = spell.spellId,
            invocationCount = spell.InvocationCount,
            loopCompleted = loopCompleted,
            loopCount = _loopCount,
            isRecast = isRecast
        };
    }

    SpellCastResult TryFlushDueMulticast(double roundTime)
    {
        if (_pendingMulticast.remainingInvocations <= 0
            || roundTime < _pendingMulticast.nextFireTime)
            return SpellCastResult.None;

        SpellCastResult result = StartInvocation(
            _pendingMulticast.spell,
            _pressOrigin,
            _pressForward,
            roundTime,
            loopCompleted: false,
            isRecast: true);
        _pendingMulticast.remainingInvocations--;
        _pendingMulticast.nextFireTime = roundTime + _pendingMulticast.cooldown;
        if (_pendingMulticast.remainingInvocations == 0)
            _pendingMulticast = default;
        return result;
    }

    void UpdateOnDeckFlags(IReadOnlyList<RuntimeSpell> spells)
    {
        int onDeck = NextCastIndex;
        for (int i = 0; i < spells.Count; i++)
            spells[i].SetOnDeck(i == onDeck);
    }

    public void Update(float simulationTime)
    {
        _emissionHandler?.Update(simulationTime);
    }

    public void Reset()
    {
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
        _pendingMulticast = default;
        _pressOrigin = default;
        _pressForward = default;
        UpdateOnDeckFlags(_spellCollection.RuntimeSpells);
    }

    public void ClearCastState()
    {
        _pendingMulticast = default;
        _emissionHandler?.ClearPendingSpawns();
    }


    public RuntimeSpell PeekNextSpell()
    {
        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        if (spells.Count == 0)
            return null;
        return spells[NextCastIndex];
    }
}
