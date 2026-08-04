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
///
/// Also the sole writer of <see cref="RuntimeSpell"/> forecast/cast state: call
/// <see cref="EvaluateForecasts"/> each frame after item evaluation so presentation can preview what each
/// upcoming spell would do under the current modifications.
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
                total += spells[i].InvocationCount;
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
    /// Casts the next spell in the loop if timing allows. Spell modifications are applied at spawn time by
    /// <see cref="AttackEntityManager"/> (fed via <see cref="SpellEmissionHandler"/>), not here.
    /// </summary>
    public SpellCastResult AttemptToCastNextSpell(double roundTime, float2 origin, bool castRequestedThisFrame)
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

        next.RecordCast(roundTime);

        _indexOfLastCast = nextIndex;
        _timeOfLastCast = roundTime;

        // Flip on-deck immediately so previews swap without waiting for the next frame's forecast pass.
        UpdateOnDeckFlags(spells);

        _spellInvoker.StartCast(next, origin, (float)roundTime, next.spellId, next.InvocationCount);

        return new SpellCastResult
        {
            didCast = true,
            spellId = next.spellId,
            invocationCount = next.InvocationCount,
            loopCompleted = loopCompleted,
            loopCount = _loopCount
        };
    }

    /// <summary>
    /// Resolves an "if cast right now" <see cref="SpellCastForecast"/> for every loop slot against
    /// <paramref name="frameModifications"/> and pushes it onto each <see cref="RuntimeSpell"/>. Each spell
    /// raises its own change events, so nothing is published when a forecast is unchanged.
    ///
    /// Call once per frame after item evaluation (while the frame's modifications are immutable) and before
    /// <see cref="AttemptToCastNextSpell"/>.
    /// </summary>
    public void EvaluateForecasts(SpellModifications frameModifications)
    {
        IReadOnlyList<RuntimeSpell> spells = _spellCollection.RuntimeSpells;
        for (int i = 0; i < spells.Count; i++)
        {
            RuntimeSpell spell = spells[i];
            spell.SetCurrentForecast(BuildForecast(spell, frameModifications));
        }

        UpdateOnDeckFlags(spells);
    }

    void UpdateOnDeckFlags(IReadOnlyList<RuntimeSpell> spells)
    {
        int onDeck = NextCastIndex;
        for (int i = 0; i < spells.Count; i++)
            spells[i].SetOnDeck(i == onDeck);
    }

    /// <summary>
    /// Builds the forecast for one slot. Spells currently emit from a single keyframe, so only the first one
    /// is read; modification math is shared with the spawn path so preview and reality agree.
    /// </summary>
    static SpellCastForecast BuildForecast(RuntimeSpell spell, SpellModifications mods)
    {
        SpellAuthoringData definition = spell.Definition;
        var forecast = new SpellCastForecast { spellId = spell.spellId };
        if (definition == null)
            return forecast;

        forecast.castTime = definition.castTime;
        forecast.castCompletionDuration = definition.castCompletionDuration;

        List<SpellKeyFrame> keyFrames = definition.SpellAnimation?.keyFrames;
        if (keyFrames == null || keyFrames.Count == 0)
            return forecast;

        SpellKeyFrame keyFrame = keyFrames[0];
        if (keyFrame == null)
            return forecast;

        forecast.spawnTime = keyFrame.time;

        SpellAttributeMask mask = definition.attributeMask;
        AttackEntityEmitter emitter = keyFrame.attackEntityEmitter;
        if (emitter != null)
        {
            forecast.emitCount = SpellModificationsApplicator.ResolveEmitCount(mods, emitter.baseEmitCount, mask);
            forecast.originOffset = emitter.relativeToPlayerSpawnCriteria.offsetFromPlayer;
            forecast.spreadDegrees = emitter.spreadDegrees;
            forecast.emitDuration = emitter.emitDuration;
            forecast.speed = emitter.speed;
        }

        AttackEntityData attackData = keyFrame.attackEntityData;
        if (attackData != null)
        {
            forecast.hitBox = AttackEntityModificationApplicator.ResolveHitBox(attackData.hitBoxData, mods, mask);
            AttackEntityModificationApplicator.ResolveDamageRanges(
                attackData, mods, mask,
                out forecast.physicalDamage,
                out forecast.coldDamage,
                out forecast.fireDamage,
                out forecast.lightningDamage);
        }

        return forecast;
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
        UpdateOnDeckFlags(_spellCollection.RuntimeSpells);
    }

    public void ClearCastState()
    {
        _spellInvoker?.ClearActiveCasts();
        _emissionHandler?.ClearPendingSpawns();
    }
}
