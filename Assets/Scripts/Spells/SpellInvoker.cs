using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Implemented by the type that handles "keyframe fired" (emit + spawn). Invoker depends on this.
/// </summary>
public interface ISpellEmissionHandler
{
    void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, SpellAuthoringData spellData, float keyframeFireTime, int spellId, int spellInvocationId);
    /// <summary>Process any pending time-delayed spawns. Call each frame after the invoker.</summary>
    void Update(float simulationTime);
}

/// <summary>
/// Manages casting only: tracks active casts and advances them by keyframe time.
/// When a keyframe is due, calls the emission handler. Does not spawn entities or hold AttackEntityManager.
/// </summary>
public class SpellInvoker
{
    private readonly ISpellEmissionHandler _emissionHandler;

    private struct ActiveCast
    {
        public float2 origin;
        public float startTime;
        public int nextKeyframeIndex;
        public SpellAuthoringData spellData;
        public int spellId;
        public int spellInvocationId;
    }

    private readonly List<ActiveCast> _activeCasts = new List<ActiveCast>();

    public SpellInvoker(ISpellEmissionHandler emissionHandler)
    {
        _emissionHandler = emissionHandler ?? throw new System.ArgumentNullException(nameof(emissionHandler));
    }

    /// <summary>
    /// Start a new cast of the given spell at the given origin. Keyframes will fire relative to startTime.
    /// </summary>
    public void StartCast(SpellAuthoringData spellData, float2 origin, float startTime, int spellId, int spellInvocationId)
    {
        if (spellData == null || spellData.SpellAnimation?.keyFrames == null || spellData.SpellAnimation.keyFrames.Count == 0)
            return;

        _activeCasts.Add(new ActiveCast
        {
            origin = origin,
            startTime = startTime,
            nextKeyframeIndex = 0,
            spellData = spellData,
            spellId = spellId,
            spellInvocationId = spellInvocationId
        });
    }

    /// <summary>
    /// Advance all active casts. When a keyframe is due, invokes OnKeyframeFired(keyFrame, origin, forward, spellData).
    /// </summary>
    /// <param name="simulationTime">Current simulation time.</param>
    /// <param name="forward">Current cast direction (e.g. player facing). Used when the handler emits; pass default e.g. (1,0) if not applicable.</param>
    public void Update(float simulationTime, float2 forward)
    {
        for (int c = _activeCasts.Count - 1; c >= 0; c--)
        {
            ActiveCast cast = _activeCasts[c];
            if (cast.spellData?.SpellAnimation?.keyFrames == null)
            {
                _activeCasts.RemoveAt(c);
                continue;
            }

            var keyFrames = cast.spellData.SpellAnimation.keyFrames;
            float elapsed = simulationTime - cast.startTime;

            while (cast.nextKeyframeIndex < keyFrames.Count
                   && elapsed >= keyFrames[cast.nextKeyframeIndex].time)
            {
                float keyframeTime = keyFrames[cast.nextKeyframeIndex].time;
                float keyframeFireTime = cast.startTime + keyframeTime;
                _emissionHandler.OnKeyframeFired(keyFrames[cast.nextKeyframeIndex], cast.origin, forward, cast.spellData, keyframeFireTime, cast.spellId, cast.spellInvocationId);
                cast.nextKeyframeIndex++;
            }

            if (cast.nextKeyframeIndex >= keyFrames.Count)
                _activeCasts.RemoveAt(c);
            else
                _activeCasts[c] = cast;
        }
    }
}
