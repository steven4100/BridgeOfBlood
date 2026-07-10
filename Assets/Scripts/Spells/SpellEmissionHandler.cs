using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Handles what happens when a spell keyframe fires: gets emit points (with time offsets) from the emitter,
/// builds an <see cref="AttackEntityBuildContext"/> from keyframe entity data (plus the frame's spell modifications),
/// then spawns at the correct times. Also tracks active sub-emitters on live parent entities and ticks them each
/// frame, spawning child entities at the parent's current position when the emission interval elapses.
/// Call Update(simulationTime) each frame to process time-delayed spawns and sub-emitter ticks; all attack spawns
/// from this frame are applied in one batch at the end of Update so the entity buffer is not reallocated mid-tick.
/// </summary>
public class SpellEmissionHandler : ISpellEmissionHandler
{
    private readonly AttackEntityManager _attackEntityManager;
    private readonly IEmissionTargetProvider _targetProvider;

    /// <summary>Spell modifications for the current frame; injected via <see cref="SetFrameModifications"/>. Immutable after item eval.</summary>
    private SpellModifications _frameModifications;

    private struct PendingSpawn
    {
        public AttackEntityBuildContext baseContext;
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
        public AttackEntityBuildContext childContext;
        public float emitInterval;
        public float startDelay;
    }

    private struct ActiveSubEmitter
    {
        public int parentEntityId;
        public AttackEntityEmitter emitter;
        public AttackEntityBuildContext childContext;
        public float emitInterval;
        public float startDelay;
        public float lastEmitSimTime;
    }

    private struct BufferedAttackSpawn
    {
        public AttackEntityBuildContext context;
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
        IEmissionTargetProvider targetProvider = null)
    {
        _attackEntityManager = attackEntityManager ?? throw new System.ArgumentNullException(nameof(attackEntityManager));
        _targetProvider = targetProvider;
    }

    public void SetFrameModifications(SpellModifications modifications)
    {
        _frameModifications = modifications;
    }

    public void OnKeyframeFired(SpellKeyFrame keyFrame, float2 origin, float2 forward, RuntimeSpell runtime, float keyframeFireTime, int spellId, int spellInvocationId, int keyframeIndex)
    {
        if (keyFrame?.attackEntityEmitter == null || keyFrame.attackEntityData == null)
            return;

        SpellAttributeMask mask = runtime?.Definition != null ? runtime.Definition.attributeMask : default;

        int count = GetEmitCount(keyFrame, mask);
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

        var baseContext = new AttackEntityBuildContext(
            keyFrame.attackEntityData, spellId, spellInvocationId, keyframeIndex,
            _frameModifications, mask, float2.zero, float2.zero);

        float speed = keyFrame.attackEntityEmitter.speed;
        if (speed < 0.0001f)
            speed = 1f;

        bool hasSubEmitter = false;
        var subReg = default(SubEmitterRegistration);
        SubEmitterBehavior subBehavior = FindSubEmitterBehavior(keyFrame.attackEntityData);
        if (subBehavior != null && subBehavior.subEmitter != null && subBehavior.subAttackEntityData != null)
        {
            hasSubEmitter = true;
            subReg = new SubEmitterRegistration
            {
                emitter = subBehavior.subEmitter,
                childContext = new AttackEntityBuildContext(
                    subBehavior.subAttackEntityData, spellId, spellInvocationId, keyframeIndex,
                    _frameModifications, mask, float2.zero, float2.zero),
                emitInterval = subBehavior.emitInterval,
                startDelay = subBehavior.startDelay
            };
        }

        for (int i = 0; i < emitPoints.Count; i++)
        {
            var point = emitPoints[i];
            float spawnTime = keyframeFireTime + point.timeOffset;
            _pending.Add(new PendingSpawn
            {
                baseContext = baseContext,
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
            float2 velocity = pending.point.direction * pending.speed;
            _bufferedSpawns.Add(new BufferedAttackSpawn
            {
                context = pending.baseContext.WithTransform(pending.point.position, velocity),
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

            FireSubEmission(sub, parent);
        }
    }

    void FireSubEmission(ActiveSubEmitter sub, AttackEntity parent)
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
            float2 velocity = emitPoints[j].direction * speed;
            _bufferedSpawns.Add(new BufferedAttackSpawn
            {
                context = sub.childContext.WithTransform(emitPoints[j].position, velocity),
                registerSubEmitter = false
            });
        }
    }

    void FlushBufferedSpawns()
    {
        for (int i = 0; i < _bufferedSpawns.Count; i++)
        {
            BufferedAttackSpawn b = _bufferedSpawns[i];
            int entityId = _attackEntityManager.Spawn(b.context);
            if (b.registerSubEmitter)
            {
                _activeSubEmitters.Add(new ActiveSubEmitter
                {
                    parentEntityId = entityId,
                    emitter = b.subEmitterReg.emitter,
                    childContext = b.subEmitterReg.childContext,
                    emitInterval = b.subEmitterReg.emitInterval,
                    startDelay = b.subEmitterReg.startDelay,
                    lastEmitSimTime = b.subEmitterLastEmitSimTime
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

    protected virtual int GetEmitCount(SpellKeyFrame keyFrame, SpellAttributeMask mask)
    {
        int baseCount = keyFrame.attackEntityEmitter != null ? keyFrame.attackEntityEmitter.baseEmitCount : 1;
        if (_frameModifications != null)
        {
            var resolved = SpellModificationsApplicator.Resolve(_frameModifications, SpellModificationProperty.Projectiles, mask);
            baseCount = Mathf.Max(1, (int)(baseCount * resolved.Multiplier) + (int)resolved.flat);
        }
        return baseCount < 1 ? 1 : baseCount;
    }
}
