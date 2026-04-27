using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Optional. Apply player stats, spell enhancements, etc. to the payload before spawning. Handler calls this when present.
/// </summary>
public interface ISpellPayloadModifier
{
    void Apply(ref AttackEntitySpawnPayload payload, RuntimeSpell runtime, SpellKeyFrame keyFrame);
}

/// <summary>
/// Handles what happens when a spell keyframe fires: gets emit points (with time offsets) from the emitter,
/// builds payload from keyframe entity data (and optional modifiers), then spawns at the correct times.
/// Also tracks active sub-emitters on live parent entities and ticks them each frame, spawning child entities
/// at the parent's current position when the emission interval elapses.
/// Call Update(simulationTime) each frame to process time-delayed spawns and sub-emitter ticks; all attack spawns
/// from this frame are applied in one batch at the end of Update so the entity buffer is not reallocated mid-tick.
/// </summary>
public class SpellEmissionHandler : ISpellEmissionHandler
{
    private readonly AttackEntityManager _attackEntityManager;
    private readonly ISpellPayloadModifier _payloadModifier;
    private readonly IEmissionTargetProvider _targetProvider;

    private struct PendingSpawn
    {
        public AttackEntitySpawnPayload basePayload;
        public float speed;
        public EmitPoint point;
        public float spawnTime;
        public bool hasSubEmitter;
        public SubEmitterRegistration subEmitterReg;
    }

    /// <summary>
    /// Pre-extracted sub-emitter data stored on PendingSpawn so we can register it when the parent entity is spawned.
    /// </summary>
    private struct SubEmitterRegistration
    {
        public AttackEntityEmitter emitter;
        public AttackEntitySpawnPayload childPayload;
        public float emitInterval;
        public float startDelay;
        public int spellId;
        public int spellInvocationId;
    }

    private struct ActiveSubEmitter
    {
        public int parentEntityId;
        public AttackEntityEmitter emitter;
        public AttackEntitySpawnPayload childPayload;
        public float emitInterval;
        public float startDelay;
        public float lastEmitSimTime;
        public int spellId;
        public int spellInvocationId;
    }

    private struct BufferedAttackSpawn
    {
        public AttackEntitySpawnPayload payload;
        public float2 position;
        public bool registerSubEmitter;
        public SubEmitterRegistration subEmitterReg;
        public float subEmitterLastEmitSimTime;
    }

    private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
    private readonly List<ActiveSubEmitter> _activeSubEmitters = new List<ActiveSubEmitter>();
    private readonly Dictionary<int, int> _entityIdToIndex = new Dictionary<int, int>();
    private readonly List<BufferedAttackSpawn> _bufferedSpawns = new List<BufferedAttackSpawn>();

    public bool HasPendingSpawns => _pending.Count > 0;

    public void ClearPendingSpawns()
    {
        _pending.Clear();
        _activeSubEmitters.Clear();
        _bufferedSpawns.Clear();
    }

    public SpellEmissionHandler(
        AttackEntityManager attackEntityManager,
        IEmissionTargetProvider targetProvider = null,
        ISpellPayloadModifier payloadModifier = null)
    {
        _attackEntityManager = attackEntityManager ?? throw new System.ArgumentNullException(nameof(attackEntityManager));
        _targetProvider = targetProvider;
        _payloadModifier = payloadModifier;
    }

    public void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, RuntimeSpell runtime, float keyframeFireTime, int spellId, int spellInvocationId, int keyframeIndex)
    {
        if (keyFrame?.attackEntityEmitter == null || keyFrame.attackEntityData == null)
            return;

        int count = GetEmitCount(keyFrame, runtime);
        var context = new SpellEmissionContext
        {
            origin = origin,
            forward = forward,
            count = count,
            targetProvider = _targetProvider
        };
        var emitPoints = keyFrame.attackEntityEmitter.GetEmitPoints(context);
        if (emitPoints.Count == 0)
            return;

        var buildContext = new AttackEntityBuildContext(spellId, spellInvocationId, keyframeIndex, keyFrame.attackEntityData.GetInstanceID());
        AttackEntitySpawnPayload basePayload = AttackEntityBuilder.Build(keyFrame.attackEntityData, buildContext);
        basePayload.spellId = spellId;
        basePayload.spellInvocationId = spellInvocationId;
        _payloadModifier?.Apply(ref basePayload, runtime, keyFrame);

        float speed = keyFrame.attackEntityEmitter.speed;
        if (speed < 0.0001f)
            speed = 1f;

        bool hasSubEmitter = false;
        var subReg = default(SubEmitterRegistration);
        SubEmitterBehavior subBehavior = FindSubEmitterBehavior(keyFrame.attackEntityData);
        if (subBehavior != null && subBehavior.subEmitter != null && subBehavior.subAttackEntityData != null)
        {
            hasSubEmitter = true;
            var childBuildContext = new AttackEntityBuildContext(spellId, spellInvocationId, keyframeIndex, subBehavior.subAttackEntityData.GetInstanceID());
            subReg = new SubEmitterRegistration
            {
                emitter = subBehavior.subEmitter,
                childPayload = AttackEntityBuilder.Build(subBehavior.subAttackEntityData, childBuildContext),
                emitInterval = subBehavior.emitInterval,
                startDelay = subBehavior.startDelay,
                spellId = spellId,
                spellInvocationId = spellInvocationId
            };
        }

        for (int i = 0; i < emitPoints.Count; i++)
        {
            var point = emitPoints[i];
            float spawnTime = keyframeFireTime + point.timeOffset;
            _pending.Add(new PendingSpawn
            {
                basePayload = basePayload,
                speed = speed,
                point = point,
                spawnTime = spawnTime,
                hasSubEmitter = hasSubEmitter,
                subEmitterReg = subReg
            });
        }
    }

    public void Update(float simulationTime)
    {
        DrainPendingSpawns(simulationTime);
        TickSubEmitters(simulationTime);
        FlushBufferedSpawns();
    }

    void DrainPendingSpawns(float simulationTime)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].spawnTime > simulationTime)
                continue;

            var pending = _pending[i];
            var payload = pending.basePayload;
            payload.velocity = pending.point.direction * pending.speed;
            _bufferedSpawns.Add(new BufferedAttackSpawn
            {
                payload = payload,
                position = pending.point.position,
                registerSubEmitter = pending.hasSubEmitter,
                subEmitterReg = pending.subEmitterReg,
                subEmitterLastEmitSimTime = simulationTime
            });

            _pending.RemoveAt(i);
        }
    }

    void TickSubEmitters(float simulationTime)
    {
        if (_activeSubEmitters.Count == 0)
            return;

        NativeArray<AttackEntity> entities = _attackEntityManager.GetEntities();
        BuildEntityIdLookup(entities);

        for (int i = _activeSubEmitters.Count - 1; i >= 0; i--)
        {
            var sub = _activeSubEmitters[i];

            if (!_entityIdToIndex.TryGetValue(sub.parentEntityId, out int parentIdx))
            {
                _activeSubEmitters.RemoveAt(i);
                continue;
            }

            AttackEntity parent = entities[parentIdx];

            if (parent.timeAlive < sub.startDelay)
                continue;

            if (simulationTime - sub.lastEmitSimTime < sub.emitInterval)
                continue;

            sub.lastEmitSimTime = simulationTime;
            _activeSubEmitters[i] = sub;

            FireSubEmission(sub, parent, simulationTime);
        }
    }

    void FireSubEmission(ActiveSubEmitter sub, AttackEntity parent, float simulationTime)
    {
        float2 forward = math.lengthsq(parent.velocity) > 0.0001f
            ? math.normalize(parent.velocity)
            : new float2(-1f, 0f);

        var context = new SpellEmissionContext
        {
            origin = parent.position,
            forward = forward,
            count = sub.emitter.baseEmitCount,
            targetProvider = _targetProvider
        };

        var emitPoints = sub.emitter.GetEmitPoints(context);
        if (emitPoints.Count == 0)
            return;

        float speed = sub.emitter.speed;
        if (speed < 0.0001f)
            speed = 1f;

        for (int j = 0; j < emitPoints.Count; j++)
        {
            var childPayload = sub.childPayload;
            childPayload.spellId = sub.spellId;
            childPayload.spellInvocationId = sub.spellInvocationId;
            childPayload.velocity = emitPoints[j].direction * speed;
            _bufferedSpawns.Add(new BufferedAttackSpawn
            {
                payload = childPayload,
                position = emitPoints[j].position,
                registerSubEmitter = false
            });
        }
    }

    void FlushBufferedSpawns()
    {
        for (int i = 0; i < _bufferedSpawns.Count; i++)
        {
            BufferedAttackSpawn b = _bufferedSpawns[i];
            int entityId = _attackEntityManager.Spawn(b.payload, b.position);
            if (b.registerSubEmitter)
            {
                _activeSubEmitters.Add(new ActiveSubEmitter
                {
                    parentEntityId = entityId,
                    emitter = b.subEmitterReg.emitter,
                    childPayload = b.subEmitterReg.childPayload,
                    emitInterval = b.subEmitterReg.emitInterval,
                    startDelay = b.subEmitterReg.startDelay,
                    lastEmitSimTime = b.subEmitterLastEmitSimTime,
                    spellId = b.subEmitterReg.spellId,
                    spellInvocationId = b.subEmitterReg.spellInvocationId
                });
            }
        }

        _bufferedSpawns.Clear();
    }

    void BuildEntityIdLookup(NativeArray<AttackEntity> entities)
    {
        _entityIdToIndex.Clear();
        for (int i = 0; i < entities.Length; i++)
            _entityIdToIndex[entities[i].entityId] = i;
    }

    static SubEmitterBehavior FindSubEmitterBehavior(AttackEntityData data)
    {
        if (data.behaviors == null) return null;
        for (int i = 0; i < data.behaviors.Count; i++)
        {
            if (data.behaviors[i] is SubEmitterBehavior sub)
                return sub;
        }
        return null;
    }

    protected virtual int GetEmitCount(SpellKeyFrame keyFrame, RuntimeSpell runtime)
    {
        int baseCount = keyFrame.attackEntityEmitter != null ? keyFrame.attackEntityEmitter.baseEmitCount : 1;
        return baseCount < 1 ? 1 : baseCount;
    }
}
