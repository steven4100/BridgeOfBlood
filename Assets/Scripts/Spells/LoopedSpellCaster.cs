using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

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
    private int _indexOfLastCast;
    private double _timeOfLastCast;

    /// <summary>
    /// Number of spells in the loop.
    /// </summary>
    public int SpellCount => _spells?.Count ?? 0;

    /// <summary>
    /// Index in the loop of the spell that was last cast (0..N-1), or -1 if none cast yet.
    /// </summary>
    public int IndexOfLastCast => _indexOfLastCast;

    public LoopedSpellCaster(IReadOnlyList<Spell> spells, SpellInvoker spellInvoker)
    {
        _spells = spells ?? new List<Spell>();
        _spellInvoker = spellInvoker;
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
    }

    /// <summary>
    /// If the user requested a cast this frame and the next spell in the loop is ready (enough time since last cast),
    /// invokes it via the SpellInvoker at the given origin and returns that spell. Otherwise returns null.
    /// </summary>
    /// <param name="castRequestedThisFrame">True when the user pressed the cast input this frame (e.g. spacebar).</param>
    public Spell AttemptToCastNextSpell(double roundTime, float2 origin, IReadOnlyList<SpellAuthoringData> spellDataList, bool castRequestedThisFrame)
    {
        if (!castRequestedThisFrame)
            return null;
        if (_spells == null || _spells.Count == 0)
            return null;
        if (spellDataList == null || spellDataList.Count == 0)
            return null;

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
            return null;

        Spell next = _spells[nextIndex];
        next.roundTimeInvokedAt = roundTime;
        next.invocationCount++;

        _indexOfLastCast = nextIndex;
        _timeOfLastCast = roundTime;

        if (nextIndex < spellDataList.Count && spellDataList[nextIndex] != null)
            _spellInvoker.StartCast(spellDataList[nextIndex], origin, (float)roundTime);

        return next;
    }

    /// <summary>
    /// Advance the invoker's active casts (keyframes fire via callback). Call each frame after AttemptToCastNextSpell.
    /// </summary>
    /// <param name="forward">Cast direction for emission (e.g. player facing).</param>
    public void Update(float simulationTime, float2 forward)
    {
        _spellInvoker?.Update(simulationTime, forward);
    }

    /// <summary>
    /// Resets the loop state so the next call will consider the first spell ready to cast.
    /// </summary>
    public void Reset()
    {
        _indexOfLastCast = -1;
        _timeOfLastCast = -1000.0;
    }
}
