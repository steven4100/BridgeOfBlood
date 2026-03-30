using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Implemented by the type that handles "keyframe fired" (emit + spawn). Invoker depends on this.
/// </summary>
public interface ISpellEmissionHandler
{
    void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, RuntimeSpell runtime, float keyframeFireTime, int spellId, int spellInvocationId);

    void Update(float simulationTime);

    bool HasPendingSpawns { get; }

    void ClearPendingSpawns();
}

/// <summary>
/// Manages casting only: tracks active casts and advances them by keyframe time.
/// When a keyframe is due, calls the emission handler. Does not spawn entities or hold AttackEntityManager.
/// </summary>
public class SpellInvoker
{
    private readonly ISpellEmissionHandler _emissionHandler;

    /// <summary>
    /// <see cref="RuntimeSpell"/> is the loop slot (identity + base <see cref="RuntimeSpell.Definition"/>).
    /// <see cref="keyframeSource"/> is the timeline we actually play: same as Definition when there are no mods,
    /// or the one-off clone from <see cref="SpellAuthoringData.Modify"/> when cast-time modifications apply (different keyframes than Definition).
    /// </summary>
    private struct ActiveCast
    {
        public float2 origin;
        public float startTime;
        public int nextKeyframeIndex;
        public RuntimeSpell runtime;
        public SpellAuthoringData keyframeSource;
        public int spellId;
        public int spellInvocationId;
    }

    private readonly List<ActiveCast> _activeCasts = new List<ActiveCast>();

    public bool HasActiveCasts => _activeCasts.Count > 0;

    public SpellInvoker(ISpellEmissionHandler emissionHandler)
    {
        _emissionHandler = emissionHandler ?? throw new System.ArgumentNullException(nameof(emissionHandler));
    }

    /// <summary>
    /// Start a new cast. <paramref name="keyframeSource"/> is the timeline to play (base definition or <see cref="SpellAuthoringData.Modify"/> clone).
    /// </summary>
    public void StartCast(RuntimeSpell runtime, SpellAuthoringData keyframeSource, float2 origin, float startTime, int spellId, int spellInvocationId)
    {
        if (runtime == null || keyframeSource == null || keyframeSource.SpellAnimation?.keyFrames == null || keyframeSource.SpellAnimation.keyFrames.Count == 0)
            return;

        _activeCasts.Add(new ActiveCast
        {
            origin = origin,
            startTime = startTime,
            nextKeyframeIndex = 0,
            runtime = runtime,
            keyframeSource = keyframeSource,
            spellId = spellId,
            spellInvocationId = spellInvocationId
        });
    }

    public void Update(float simulationTime, float2 forward)
    {
        for (int c = _activeCasts.Count - 1; c >= 0; c--)
        {
            ActiveCast cast = _activeCasts[c];
            if (cast.keyframeSource?.SpellAnimation?.keyFrames == null)
            {
                _activeCasts.RemoveAt(c);
                continue;
            }

            var keyFrames = cast.keyframeSource.SpellAnimation.keyFrames;
            float elapsed = simulationTime - cast.startTime;

            while (cast.nextKeyframeIndex < keyFrames.Count
                   && elapsed >= keyFrames[cast.nextKeyframeIndex].time)
            {
                float keyframeTime = keyFrames[cast.nextKeyframeIndex].time;
                float keyframeFireTime = cast.startTime + keyframeTime;
                _emissionHandler.OnKeyframeFired(keyFrames[cast.nextKeyframeIndex], cast.origin, forward, cast.runtime, keyframeFireTime, cast.spellId, cast.spellInvocationId);
                cast.nextKeyframeIndex++;
            }

            if (cast.nextKeyframeIndex >= keyFrames.Count)
                _activeCasts.RemoveAt(c);
            else
                _activeCasts[c] = cast;
        }
    }

    public void ClearActiveCasts()
    {
        _activeCasts.Clear();
    }
}
