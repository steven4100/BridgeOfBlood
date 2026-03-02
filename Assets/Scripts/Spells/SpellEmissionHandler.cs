using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Optional. Apply player stats, spell enhancements, etc. to the payload before spawning. Handler calls this when present.
/// </summary>
public interface ISpellPayloadModifier
{
    void Apply(ref AttackEntitySpawnPayload payload, SpellAuthoringData spellData, SpellKeyFrame keyFrame);
}

/// <summary>
/// Handles what happens when a spell keyframe fires: gets emit points (with time offsets) from the emitter,
/// builds payload from keyframe entity data (and optional modifiers), then spawns at the correct times.
/// Call Update(simulationTime) each frame to process time-delayed spawns.
/// </summary>
public class SpellEmissionHandler : ISpellEmissionHandler
{
    private readonly AttackEntityManager _attackEntityManager;
    private readonly ISpellPayloadModifier _payloadModifier;

    private struct PendingSpawn
    {
        public AttackEntitySpawnPayload basePayload;
        public float speed;
        public EmitPoint point;
        public float spawnTime;
    }

    private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();

    public SpellEmissionHandler(
        AttackEntityManager attackEntityManager,
        ISpellPayloadModifier payloadModifier = null)
    {
        _attackEntityManager = attackEntityManager ?? throw new System.ArgumentNullException(nameof(attackEntityManager));
        _payloadModifier = payloadModifier;
    }

    public void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, SpellAuthoringData spellData, float keyframeFireTime)
    {
        if (keyFrame?.attackEntityEmitter == null || keyFrame.attackEntityData == null)
            return;

        int count = GetEmitCount(keyFrame, spellData);
        var emitPoints = keyFrame.attackEntityEmitter.GetEmitPoints(origin, forward, count);
        if (emitPoints.Count == 0)
            return;

        AttackEntitySpawnPayload basePayload = AttackEntityBuilder.Build(keyFrame.attackEntityData);
        _payloadModifier?.Apply(ref basePayload, spellData, keyFrame);

        float speed = keyFrame.attackEntityEmitter.speed;
        if (speed < 0.0001f)
            speed = 1f;

        for (int i = 0; i < emitPoints.Count; i++)
        {
            var point = emitPoints[i];
            float spawnTime = keyframeFireTime + point.timeOffset;
            _pending.Add(new PendingSpawn
            {
                basePayload = basePayload,
                speed = speed,
                point = point,
                spawnTime = spawnTime
            });
        }
    }

    public void Update(float simulationTime)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].spawnTime > simulationTime)
                continue;

            var pending = _pending[i];
            var payload = pending.basePayload;
            // Use emitter direction per point (so spread/fan works) with magnitude from authored velocity.
            payload.velocity = pending.point.direction * pending.speed;
            _attackEntityManager.Spawn(payload, pending.point.position);
            _pending.RemoveAt(i);
        }
    }

    /// <summary>
    /// Number of emit points per keyframe. Uses emitter's baseEmitCount; override to add spell/item modifiers later.
    /// </summary>
    protected virtual int GetEmitCount(SpellKeyFrame keyFrame, SpellAuthoringData spellData)
    {
        int baseCount = keyFrame.attackEntityEmitter != null ? keyFrame.attackEntityEmitter.baseEmitCount : 1;
        return baseCount < 1 ? 1 : baseCount;
    }
}
