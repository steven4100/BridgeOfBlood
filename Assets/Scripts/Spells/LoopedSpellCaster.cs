using System.Collections.Generic;
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
    private readonly IReadOnlyList<Spell> _spells;
    private readonly SpellInvoker _spellInvoker;
    private readonly ISpellEmissionHandler _emissionHandler;
    private int _indexOfLastCast;
    private double _timeOfLastCast;
    private int _loopCount;

    /// <summary>
    /// Number of spells in the loop.
    /// </summary>
    public int SpellCount => _spells?.Count ?? 0;

    /// <summary>
    /// Index in the loop of the spell that was last cast (0..N-1), or -1 if none cast yet.
    /// </summary>
    public int IndexOfLastCast => _indexOfLastCast;

    /// <summary>
    /// Total number of completed spell loops.
    /// </summary>
    public int LoopCount => _loopCount;

    /// <summary>
    /// True when the SpellInvoker has active casts that haven't finished firing keyframes.
    /// </summary>
    public bool HasActiveCasts => _spellInvoker != null && _spellInvoker.HasActiveCasts;

    /// <summary>
    /// True when the SpellEmissionHandler has pending delayed spawns.
    /// </summary>
    public bool HasPendingSpawns => _emissionHandler != null && _emissionHandler.HasPendingSpawns;

    /// <summary>
    /// Creates a spell caster that owns the given emission handler and an internal SpellInvoker.
    /// </summary>
    public LoopedSpellCaster(IReadOnlyList<Spell> spells, ISpellEmissionHandler emissionHandler)
    {
        _spells = spells ?? new List<Spell>();
        _emissionHandler = emissionHandler ?? throw new System.ArgumentNullException(nameof(emissionHandler));
        _spellInvoker = new SpellInvoker(_emissionHandler);
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
    }

    /// <summary>
    /// If the user requested a cast this frame and the next spell in the loop is ready (enough time since last cast),
    /// invokes it via the SpellInvoker at the given origin and returns a <see cref="SpellCastResult"/>.
    /// When modifications is non-null, the spell is cast as spell.Modify(modifications) so modifications are applied.
    /// </summary>
    /// <param name="castRequestedThisFrame">True when the user pressed the cast input this frame (e.g. spacebar).</param>
    /// <param name="modifications">Optional. If set, applied to the spell before casting (via SpellAuthoringData.Modify).</param>
    public SpellCastResult AttemptToCastNextSpell(double roundTime, float2 origin, IReadOnlyList<SpellAuthoringData> spellDataList, bool castRequestedThisFrame, SpellModifications modifications = null)
    {
        if (!castRequestedThisFrame)
            return SpellCastResult.None;
        if (_spells == null || _spells.Count == 0)
            return SpellCastResult.None;
        if (spellDataList == null || spellDataList.Count == 0)
            return SpellCastResult.None;

        int nextIndex = (_indexOfLastCast + 1) % _spells.Count;
        bool canCastNext;

        if (_indexOfLastCast < 0)
        {
            canCastNext = true;
        }
        else
        {
            Spell lastSpell = _spells[_indexOfLastCast];
            double requiredElapsed = lastSpell.castCompletionDuration;
            canCastNext = (roundTime - _timeOfLastCast) >= requiredElapsed;
        }

        if (!canCastNext)
            return SpellCastResult.None;

        bool loopCompleted = nextIndex == 0 && _indexOfLastCast >= 0;
        if (loopCompleted)
            _loopCount++;

        Spell next = _spells[nextIndex];
        next.roundTimeInvokedAt = roundTime;
        next.invocationCount++;

        _indexOfLastCast = nextIndex;
        _timeOfLastCast = roundTime;

        if (nextIndex < spellDataList.Count && spellDataList[nextIndex] != null)
        {
            SpellAuthoringData spellToCast = modifications != null
                ? spellDataList[nextIndex].Modify(modifications)
                : spellDataList[nextIndex];
            _spellInvoker.StartCast(spellToCast, origin, (float)roundTime, next.spellId, next.invocationCount);
        }

        return new SpellCastResult
        {
            didCast = true,
            spellId = next.spellId,
            invocationCount = next.invocationCount,
            loopCompleted = loopCompleted,
            loopCount = _loopCount
        };
    }

    /// <summary>
    /// Advance the invoker's active casts (keyframes fire via callback) and the emission handler's pending spawns. Call each frame after AttemptToCastNextSpell.
    /// </summary>
    /// <param name="forward">Cast direction for emission (e.g. player facing).</param>
    public void Update(float simulationTime, float2 forward)
    {
        _spellInvoker?.Update(simulationTime, forward);
        _emissionHandler?.Update(simulationTime);
    }

    /// <summary>
    /// Resets the loop state so the next call will consider the first spell ready to cast.
    /// </summary>
    public void Reset()
    {
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
        _loopCount = 0;
    }

    /// <summary>
    /// Clears all active casts and pending spawns.
    /// </summary>
    public void ClearCastState()
    {
        _spellInvoker?.ClearActiveCasts();
        _emissionHandler?.ClearPendingSpawns();
    }
}
