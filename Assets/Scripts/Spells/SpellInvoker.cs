using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Implemented by the type that handles "keyframe fired" (emit + spawn). Invoker depends on this.
/// </summary>
public interface ISpellEmissionHandler
{
    void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, RuntimeSpell runtime, float keyframeFireTime, int spellId, int spellInvocationId, int keyframeIndex);

    /// <summary>
    /// Sets the spell modifications applied to attack entities spawned this frame. Injected by the round/lab
    /// each frame after item evaluation; must be treated as immutable for the rest of the frame (delayed and
    /// sub-emitter spawns keep this reference as their snapshot).
    /// </summary>
    void SetFrameModifications(SpellModifications modifications);

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
    /// <see cref="RuntimeSpell"/> is the loop slot (identity + <see cref="RuntimeSpell.Definition"/>), and its
    /// <see cref="RuntimeSpell.Definition"/> is the timeline we play directly. Spell modifications are applied
    /// at spawn time by <see cref="AttackEntityManager"/>, so the invoker never clones or modifies the spell.
    /// </summary>
    private struct ActiveCast
    {
        public float2 origin;
        public float startTime;
        public int nextKeyframeIndex;
        public RuntimeSpell runtime;
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
    /// Start a new cast. The timeline played is <paramref name="runtime"/>'s <see cref="RuntimeSpell.Definition"/>.
    /// </summary>
    public void StartCast(RuntimeSpell runtime, float2 origin, float startTime, int spellId, int spellInvocationId)
    {
        if (runtime?.Definition == null || runtime.Definition.SpellAnimation?.keyFrames == null || runtime.Definition.SpellAnimation.keyFrames.Count == 0)
            return;

        _activeCasts.Add(new ActiveCast
        {
            origin = origin,
            startTime = startTime,
            nextKeyframeIndex = 0,
            runtime = runtime,
            spellId = spellId,
            spellInvocationId = spellInvocationId
        });
    }

    public void Update(float simulationTime, float2 forward)
    {
        for (int c = _activeCasts.Count - 1; c >= 0; c--)
        {
            ActiveCast cast = _activeCasts[c];
            if (cast.runtime?.Definition?.SpellAnimation?.keyFrames == null)
            {
                _activeCasts.RemoveAt(c);
                continue;
            }

            var keyFrames = cast.runtime.Definition.SpellAnimation.keyFrames;
            float elapsed = simulationTime - cast.startTime;

            while (cast.nextKeyframeIndex < keyFrames.Count
                   && elapsed >= keyFrames[cast.nextKeyframeIndex].time)
            {
                int keyframeIndex = cast.nextKeyframeIndex;
                float keyframeTime = keyFrames[keyframeIndex].time;
                float keyframeFireTime = cast.startTime + keyframeTime;
                _emissionHandler.OnKeyframeFired(keyFrames[keyframeIndex], cast.origin, forward, cast.runtime, keyframeFireTime, cast.spellId, cast.spellInvocationId, keyframeIndex);
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
