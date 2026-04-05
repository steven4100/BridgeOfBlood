using System;
using UnityEngine;

/// <summary>
/// Optional behavior on <see cref="AttackEntityData"/> that causes each live instance to periodically
/// sub-emit child attack entities from its current position. The emission pattern is defined by a reusable
/// <see cref="AttackEntityEmitter"/> (spread, targeting mode, speed, count) and the child entity by a
/// separate <see cref="AttackEntityData"/>.
/// <para>
/// <see cref="ApplyTo"/> is a no-op: sub-emitter data is an emission concern handled by
/// <see cref="SpellEmissionHandler"/>, not a property of the parent entity's spawn payload.
/// </para>
/// </summary>
[Serializable]
public class SubEmitterBehavior : AttackEntityBehavior
{
    [Tooltip("Emission pattern for sub-emitted children (spread, speed, targeting mode, etc.).")]
    public AttackEntityEmitter subEmitter;

    [Tooltip("Entity to spawn as sub-emission.")]
    public AttackEntityData subAttackEntityData;

    [Tooltip("Seconds between sub-emissions. Each fire evaluates context fresh.")]
    [Min(0.01f)]
    public float emitInterval = 0.5f;

    [Tooltip("Seconds after parent spawns before first sub-emission.")]
    [Min(0f)]
    public float startDelay = 0f;

    public override void ApplyTo(ref AttackEntitySpawnPayload payload) { }

    public override AttackEntityBehavior Clone() => new SubEmitterBehavior
    {
        subEmitter = CloneEmitter(subEmitter),
        subAttackEntityData = subAttackEntityData,
        emitInterval = emitInterval,
        startDelay = startDelay
    };

    static AttackEntityEmitter CloneEmitter(AttackEntityEmitter source)
    {
        if (source == null) return null;
        return new AttackEntityEmitter
        {
            targetMode = source.targetMode,
            spreadDegrees = source.spreadDegrees,
            forwardDegrees = source.forwardDegrees,
            emitDuration = source.emitDuration,
            baseEmitCount = source.baseEmitCount,
            speed = source.speed,
            relativeToPlayerSpawnCriteria = source.relativeToPlayerSpawnCriteria,
            targetRange = source.targetRange
        };
    }
}
