using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Returned by <see cref="LoopedSpellCaster.AttemptToCastNextSpell"/> each frame.
/// Tells the caller exactly what happened: whether a spell was cast, which one,
/// and whether the loop just completed.
/// </summary>
public struct SpellCastResult
{
    public bool didCast;
    public int spellId;
    public int invocationCount;
    public bool loopCompleted;
    public int loopCount;

    public static readonly SpellCastResult None = default;
}

/// <summary>
/// Drives a fixed loop of N spells. Enforces cast-completion timing: the next spell in the loop
/// may only be cast after the current spell's castCompletionDuration has elapsed.
/// When it's time to cast, uses its SpellInvoker to run the spell animation.
/// Plain class — call AttemptToCastNextSpell each frame; call Update each frame to advance invoked casts.
/// </summary>
public class LoopedSpellCaster
{
    private readonly SpellCollection _spellCollection;
    private readonly SpellInvoker _spellInvoker;
    private readonly ISpellEmissionHandler _emissionHandler;
    private int _indexOfLastCast;
    private double _timeOfLastCast;
    private int _loopCount;

    public IReadOnlyList<RuntimeSpell> Spells => _spellCollection.RuntimeSpells;

    public int SpellCount => _spellCollection.Count;

    public int IndexOfLastCast => _indexOfLastCast;

    public int LoopCount => _loopCount;

    public int NextCastIndex => _spellCollection.Count > 0
        ? (_indexOfLastCast + 1) % _spellCollection.Count
        : -1;

    public int TotalInvocationCount
    {
        get
        {
            IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
            int total = 0;
            for (int i = 0; i < spells.Count; i++)
                total += spells[i].invocationCount;
            return total;
        }
    }

    public bool HasActiveCasts => _spellInvoker != null && _spellInvoker.HasActiveCasts;

    public bool HasPendingSpawns => _emissionHandler != null && _emissionHandler.HasPendingSpawns;

    public SpellAttributeMask GetSpellAttributeMask(int loopIndex)
    {
        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        if (loopIndex < 0 || loopIndex >= spells.Count)
            return SpellAttributeMask.None;
        var def = spells[loopIndex].Definition;
        return def != null ? def.attributeMask : SpellAttributeMask.None;
    }

    public LoopedSpellCaster(SpellCollection spellCollection, ISpellEmissionHandler emissionHandler)
    {
        _spellCollection = spellCollection;
        _emissionHandler = emissionHandler ?? throw new System.ArgumentNullException(nameof(emissionHandler));
        _spellInvoker = new SpellInvoker(_emissionHandler);
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
    }

    /// <summary>
    /// When <paramref name="modifications"/> is non-null, applies them via <see cref="SpellAuthoringData.Modify"/> for this cast's keyframe timeline.
    /// </summary>
    public SpellCastResult AttemptToCastNextSpell(double roundTime, float2 origin, bool castRequestedThisFrame, SpellModifications modifications = null)
    {
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
            double requiredElapsed = last.Definition != null ? last.Definition.castCompletionDuration : 0;
            canCastNext = (roundTime - _timeOfLastCast) >= requiredElapsed;
        }

        if (!canCastNext)
            return SpellCastResult.None;

        bool loopCompleted = nextIndex == 0 && _indexOfLastCast >= 0;
        if (loopCompleted)
            _loopCount++;

        RuntimeSpell next = spells[nextIndex];
        if (next.Definition == null)
            return SpellCastResult.None;

        next.roundTimeInvokedAt = roundTime;
        next.invocationCount++;

        _indexOfLastCast = nextIndex;
        _timeOfLastCast = roundTime;

        SpellAuthoringData keyframeSource = modifications != null
            ? next.Definition.Modify(modifications)
            : next.Definition;
        _spellInvoker.StartCast(next, keyframeSource, origin, (float)roundTime, next.spellId, next.invocationCount);

        return new SpellCastResult
        {
            didCast = true,
            spellId = next.spellId,
            invocationCount = next.invocationCount,
            loopCompleted = loopCompleted,
            loopCount = _loopCount
        };
    }

    public void Update(float simulationTime, float2 forward)
    {
        _spellInvoker?.Update(simulationTime, forward);
        _emissionHandler?.Update(simulationTime);
    }

    public void Reset()
    {
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
    }

    public void ClearCastState()
    {
        _spellInvoker?.ClearActiveCasts();
        _emissionHandler?.ClearPendingSpawns();
    }
}
