using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

/// <summary>
/// Runtime state of a live attack entity (projectile, AoE, etc.).
/// Spawned from an AttackEntityBuildContext when a spell keyframe fires.
/// </summary>
public struct AttackEntity
{
    public EntityId entityId;
    public float2 position;
    public float2 velocity;
    public float timeAlive;
    /// <summary>Increments once per attack time tick (same cadence as <see cref="timeAlive"/>).</summary>
    public int framesAlive;
    public float distanceTravelled;
    public int enemiesHit;
    public float rehitCooldownSeconds;

    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public float critChance;
    public float critDamageMultiplier;
    public float knockbackStrength;
    public HitBoxData hitBox;
    public float currentHitBoxScale;
    public EntityVisual visual;
    public AudioUnitRuntime onDamageSound;
    public EffectSpriteConfigRuntime onHitEffect;
    public EffectSpriteConfigRuntime onKillEffect;
    public int spellId;
    public int spellInvocationId;
}

/// <summary>
/// Manages live attack entities. Spawns from an <see cref="AttackEntityBuildContext"/> (authoring data + mods);
/// removal is driven by removal events from PierceSystem, ExpirationSystem, etc., resolved via ApplyRemovals at end of frame.
/// Stable slot pool: removing an entity tombstones the slot (no swap-back); reuse bumps generation so stale
/// <see cref="EntityId"/> handles fail <see cref="IsValid"/>. Parallel policy lists stay index-aligned with slots.
/// Call ValidateParallelLists / ValidateHitEvents (code owner) before passing data to systems; systems assume valid input.
/// </summary>
public class AttackEntityManager
{
    private NativeList<AttackEntity> _entities;
    private NativeList<byte> _alive;
    private NativeList<uint> _generations;
    private NativeList<int> _freeSlots;
    private NativeList<ChainPolicyRuntime> _chainPolicies;
    private NativeList<PiercePolicyRuntime> _piercePolicies;
    private NativeList<ExpirationPolicyRuntime> _expirationPolicies;
    private NativeList<RehitPolicyRuntime> _rehitPolicies;
    private NativeList<FrozenApplierRuntime> _frozenAppliers;
    private NativeList<IgnitedApplierRuntime> _ignitedAppliers;
    private NativeList<ShockedApplierRuntime> _shockedAppliers;
    private NativeList<PoisonedApplierRuntime> _poisonedAppliers;
    private NativeList<StunnedApplierRuntime> _stunnedAppliers;
    private NativeList<BleedApplierRuntime> _bleedAppliers;
    private NativeList<MotionPolicyRuntime> _motionPolicies;
    private int _aliveCount;

    /// <summary>
    /// Hit-conditional modifier sets, keyed by entity id. Snapshotted from <see cref="SpellModifications.attackEntityModifiers"/>
    /// at spawn so live entities carry the conditionals that were active when they were created. Read by
    /// <see cref="DamageSystem"/> via <see cref="HitModifierSets"/> at hit time.
    /// </summary>
    private readonly Dictionary<EntityId, List<AttackEntityModifier>> _hitModifierSets = new Dictionary<EntityId, List<AttackEntityModifier>>();

    public AttackEntityManager()
    {
        _entities = new NativeList<AttackEntity>(Allocator.Persistent);
        _alive = new NativeList<byte>(Allocator.Persistent);
        _generations = new NativeList<uint>(Allocator.Persistent);
        _freeSlots = new NativeList<int>(Allocator.Persistent);
        _chainPolicies = new NativeList<ChainPolicyRuntime>(Allocator.Persistent);
        _piercePolicies = new NativeList<PiercePolicyRuntime>(Allocator.Persistent);
        _expirationPolicies = new NativeList<ExpirationPolicyRuntime>(Allocator.Persistent);
        _rehitPolicies = new NativeList<RehitPolicyRuntime>(Allocator.Persistent);
        _frozenAppliers = new NativeList<FrozenApplierRuntime>(Allocator.Persistent);
        _ignitedAppliers = new NativeList<IgnitedApplierRuntime>(Allocator.Persistent);
        _shockedAppliers = new NativeList<ShockedApplierRuntime>(Allocator.Persistent);
        _poisonedAppliers = new NativeList<PoisonedApplierRuntime>(Allocator.Persistent);
        _stunnedAppliers = new NativeList<StunnedApplierRuntime>(Allocator.Persistent);
        _bleedAppliers = new NativeList<BleedApplierRuntime>(Allocator.Persistent);
        _motionPolicies = new NativeList<MotionPolicyRuntime>(Allocator.Persistent);
        _aliveCount = 0;
    }

    /// <summary>
    /// Read-only view of hit-conditional modifier sets keyed by entity id. Consumed by <see cref="DamageSystem"/> at hit time.
    /// </summary>
    public IReadOnlyDictionary<EntityId, List<AttackEntityModifier>> HitModifierSets => _hitModifierSets;

    /// <summary>
    /// Spawns a new attack entity from <paramref name="ctx"/>. Rolls stats + applies spell modifications via
    /// <see cref="AttackEntityModificationApplicator"/>, writes into an allocated slot, then lets each authoring
    /// behavior write its contribution into the parallel lists by index. No intermediate payload struct.
    /// </summary>
    public EntityId Spawn(in AttackEntityBuildContext ctx)
    {
        EntityId id = AllocateSlot();
        int idx = id.Index;

        AttackEntity entity = AttackEntityModificationApplicator.BuildRolledEntity(in ctx, id);
        if (ctx.eventScaledDamage > 0f)
            AttackEntityModificationApplicator.ApplyEventScaledDamage(ref entity, ctx.eventScaledDamage);

        _entities[idx] = entity;

        var rehit = RehitPolicyRuntime.Default();
        rehit.rehitCooldownSeconds = ctx.data.rehitCooldownSeconds;
        _rehitPolicies[idx] = rehit;

        var behaviors = ctx.data.behaviors;
        if (behaviors != null)
        {
            for (int i = 0; i < behaviors.Count; i++)
                behaviors[i]?.ApplyTo(this, idx, ctx.modifications, ctx.attributeMask);
        }

        var hitModifiers = ctx.modifications?.attackEntityModifiers;
        if (hitModifiers != null && hitModifiers.Count > 0)
            _hitModifierSets[id] = hitModifiers;

        return id;
    }

    /// <summary>
    /// Returns a read-write view of the entity slot list (includes tombstones). Valid until next list modification.
    /// </summary>
    public NativeArray<AttackEntity> GetEntities()
    {
        return _entities.AsArray();
    }

    public NativeArray<byte> GetAlive() => _alive.AsArray();
    public NativeArray<uint> GetGenerations() => _generations.AsArray();

    /// <summary>
    /// Returns a read-only view of the chain policy list. Same length and index alignment as GetEntities().
    /// </summary>
    public NativeArray<ChainPolicyRuntime> GetChainPolicies()
    {
        return _chainPolicies.AsArray();
    }

    /// <summary>
    /// Returns a read-only view of the pierce policy list. Same length and index alignment as GetEntities().
    /// </summary>
    public NativeArray<PiercePolicyRuntime> GetPiercePolicies()
    {
        return _piercePolicies.AsArray();
    }

    /// <summary>
    /// Returns a read-only view of the expiration policy list. Same length and index alignment as GetEntities().
    /// </summary>
    public NativeArray<ExpirationPolicyRuntime> GetExpirationPolicies()
    {
        return _expirationPolicies.AsArray();
    }

    /// <summary>
    /// Returns a read-write view of the rehit policy list. Same length and index alignment as GetEntities().
    /// </summary>
    public NativeArray<RehitPolicyRuntime> GetRehitPolicies()
    {
        return _rehitPolicies.AsArray();
    }

    public NativeArray<FrozenApplierRuntime> GetFrozenAppliers() => _frozenAppliers.AsArray();
    public NativeArray<IgnitedApplierRuntime> GetIgnitedAppliers() => _ignitedAppliers.AsArray();
    public NativeArray<ShockedApplierRuntime> GetShockedAppliers() => _shockedAppliers.AsArray();
    public NativeArray<PoisonedApplierRuntime> GetPoisonedAppliers() => _poisonedAppliers.AsArray();
    public NativeArray<StunnedApplierRuntime> GetStunnedAppliers() => _stunnedAppliers.AsArray();
    public NativeArray<BleedApplierRuntime> GetBleedAppliers() => _bleedAppliers.AsArray();

    /// <summary>
    /// Returns a read-write view of the motion policy list. Same length and index alignment as GetEntities().
    /// </summary>
    public NativeArray<MotionPolicyRuntime> GetMotionPolicies() => _motionPolicies.AsArray();

    /// <summary>Number of live attack entities (excludes tombstones).</summary>
    public int EntityCount => _aliveCount;
    public int AliveCount => _aliveCount;
    /// <summary>Slot capacity including tombstones; equals GetEntities().Length.</summary>
    public int SlotCount => _entities.Length;

    public bool IsValid(EntityId id) =>
        id.Index >= 0
        && id.Index < _generations.Length
        && _alive[id.Index] != 0
        && _generations[id.Index] == id.Generation;

    public bool IsLive(int index) =>
        index >= 0 && index < _alive.Length && _alive[index] != 0;

    public EntityId GetEntityId(int index) => new EntityId
    {
        Index = index,
        Generation = _generations[index]
    };

    /// <summary>
    /// Validates that entity and policy lists have matching lengths. Call before passing arrays to systems.
    /// Throws if inconsistent (indicates internal bug).
    /// </summary>
    public void ValidateParallelLists()
    {
        int n = _entities.Length;
        if (_alive.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: alive.Length ({_alive.Length}) != entities.Length ({n}).");
        if (_generations.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: generations.Length ({_generations.Length}) != entities.Length ({n}).");
        if (_chainPolicies.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: chainPolicies.Length ({_chainPolicies.Length}) != entities.Length ({n}).");
        if (_piercePolicies.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: piercePolicies.Length ({_piercePolicies.Length}) != entities.Length ({n}).");
        if (_expirationPolicies.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: expirationPolicies.Length ({_expirationPolicies.Length}) != entities.Length ({n}).");
        if (_rehitPolicies.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: rehitPolicies.Length ({_rehitPolicies.Length}) != entities.Length ({n}).");
        if (_frozenAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: frozenAppliers.Length ({_frozenAppliers.Length}) != entities.Length ({n}).");
        if (_ignitedAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: ignitedAppliers.Length ({_ignitedAppliers.Length}) != entities.Length ({n}).");
        if (_shockedAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: shockedAppliers.Length ({_shockedAppliers.Length}) != entities.Length ({n}).");
        if (_poisonedAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: poisonedAppliers.Length ({_poisonedAppliers.Length}) != entities.Length ({n}).");
        if (_stunnedAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: stunnedAppliers.Length ({_stunnedAppliers.Length}) != entities.Length ({n}).");
        if (_bleedAppliers.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: bleedAppliers.Length ({_bleedAppliers.Length}) != entities.Length ({n}).");
        if (_motionPolicies.Length != n)
            throw new InvalidOperationException($"AttackEntityManager: motionPolicies.Length ({_motionPolicies.Length}) != entities.Length ({n}).");
    }

    /// <summary>
    /// Records each hit into the attack entity's rehit list (enemyId, timeAlive) so the next frame's resolver can reject rehits within cooldown.
    /// Call after DamageSystem.ProcessHits. Only updates entities with rehitCooldownSeconds > 0.
    /// </summary>
    public void RecordRehitHits(
        NativeArray<HitEvent>.ReadOnly hitEvents,
        NativeArray<AttackEntity>.ReadOnly attackEntities)
    {
        RehitRecordSystem.RecordRehitHits(hitEvents, attackEntities, _rehitPolicies.AsArray());
    }

    /// <summary>
    /// Validates that all hit events reference valid attack-entity and enemy ids. Call before passing hitEvents to ChainSystem/DamageSystem.
    /// Throws if any index is out of range, the attack slot is not live, or an id is stale (indicates upstream bug).
    /// </summary>
    public void ValidateHitEvents(NativeArray<HitEvent>.ReadOnly hitEvents, int enemySlotCount)
    {
        int slotCount = _entities.Length;
        int chainCount = _chainPolicies.Length;
        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];
            int ai = hit.attackEntityId.Index;
            int ei = hit.enemyEntityId.Index;
            if (ai < 0 || ai >= slotCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].attackEntityId.Index={ai} is out of range [0, {slotCount}).");
            if (_alive[ai] == 0)
                throw new InvalidOperationException($"HitEvent[{i}].attackEntityId={hit.attackEntityId} references a dead attack slot.");
            if (!IsValid(hit.attackEntityId))
                throw new InvalidOperationException($"HitEvent[{i}].attackEntityId={hit.attackEntityId} is stale.");
            if (ai >= chainCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].attackEntityId.Index={ai} is out of range for chainPolicies.Length={chainCount}.");
            if (ei < 0 || ei >= enemySlotCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].enemyEntityId.Index={ei} is out of range [0, {enemySlotCount}).");
        }
    }

    /// <summary>
    /// Applies pending removal events: tombstones each listed entity. Call at end of simulation after
    /// pierce/expiration (and any other) systems have appended to the list. Does not clear the list; caller should clear after.
    /// </summary>
    public void ApplyRemovals(NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        for (int i = 0; i < removalEvents.Length; i++)
            RemoveEntity(removalEvents[i].entityId);
    }

    /// <summary>
    /// Tombstones a single entity by id. No-op if the handle is stale.
    /// </summary>
    public void RemoveEntity(EntityId entityId)
    {
        if (!IsValid(entityId))
            return;

        int index = entityId.Index;
        _hitModifierSets.Remove(entityId);
        _alive[index] = 0;
        _entities[index] = default;
        _chainPolicies[index] = ChainPolicyRuntime.Default();
        _piercePolicies[index] = PiercePolicyRuntime.Default();
        _expirationPolicies[index] = ExpirationPolicyRuntime.Default();
        _rehitPolicies[index] = RehitPolicyRuntime.Default();
        _frozenAppliers[index] = FrozenApplierRuntime.Default();
        _ignitedAppliers[index] = IgnitedApplierRuntime.Default();
        _shockedAppliers[index] = ShockedApplierRuntime.Default();
        _poisonedAppliers[index] = PoisonedApplierRuntime.Default();
        _stunnedAppliers[index] = StunnedApplierRuntime.Default();
        _bleedAppliers[index] = BleedApplierRuntime.Default();
        _motionPolicies[index] = MotionPolicyRuntime.Default();
        _freeSlots.Add(index);
        _aliveCount--;
    }

    /// <summary>
    /// Removes all attack entities.
    /// </summary>
    public void Clear()
    {
        _hitModifierSets.Clear();
        _entities.Clear();
        _alive.Clear();
        _generations.Clear();
        _freeSlots.Clear();
        _chainPolicies.Clear();
        _piercePolicies.Clear();
        _expirationPolicies.Clear();
        _rehitPolicies.Clear();
        _frozenAppliers.Clear();
        _ignitedAppliers.Clear();
        _shockedAppliers.Clear();
        _poisonedAppliers.Clear();
        _stunnedAppliers.Clear();
        _bleedAppliers.Clear();
        _motionPolicies.Clear();
        _aliveCount = 0;
    }

    public void Dispose()
    {
        if (_entities.IsCreated) _entities.Dispose();
        if (_alive.IsCreated) _alive.Dispose();
        if (_generations.IsCreated) _generations.Dispose();
        if (_freeSlots.IsCreated) _freeSlots.Dispose();
        if (_chainPolicies.IsCreated) _chainPolicies.Dispose();
        if (_piercePolicies.IsCreated) _piercePolicies.Dispose();
        if (_expirationPolicies.IsCreated) _expirationPolicies.Dispose();
        if (_rehitPolicies.IsCreated) _rehitPolicies.Dispose();
        if (_frozenAppliers.IsCreated) _frozenAppliers.Dispose();
        if (_ignitedAppliers.IsCreated) _ignitedAppliers.Dispose();
        if (_shockedAppliers.IsCreated) _shockedAppliers.Dispose();
        if (_poisonedAppliers.IsCreated) _poisonedAppliers.Dispose();
        if (_stunnedAppliers.IsCreated) _stunnedAppliers.Dispose();
        if (_bleedAppliers.IsCreated) _bleedAppliers.Dispose();
        if (_motionPolicies.IsCreated) _motionPolicies.Dispose();
    }

    private EntityId AllocateSlot()
    {
        int index;
        if (_freeSlots.Length > 0)
        {
            int last = _freeSlots.Length - 1;
            index = _freeSlots[last];
            _freeSlots.RemoveAt(last);

            uint generation = _generations[index] + 1u;
            _generations[index] = generation != 0u ? generation : 1u;
            _alive[index] = 1;

            _chainPolicies[index] = ChainPolicyRuntime.Default();
            _piercePolicies[index] = PiercePolicyRuntime.Default();
            _expirationPolicies[index] = ExpirationPolicyRuntime.Default();
            _rehitPolicies[index] = RehitPolicyRuntime.Default();
            _frozenAppliers[index] = FrozenApplierRuntime.Default();
            _ignitedAppliers[index] = IgnitedApplierRuntime.Default();
            _shockedAppliers[index] = ShockedApplierRuntime.Default();
            _poisonedAppliers[index] = PoisonedApplierRuntime.Default();
            _stunnedAppliers[index] = StunnedApplierRuntime.Default();
            _bleedAppliers[index] = BleedApplierRuntime.Default();
            _motionPolicies[index] = MotionPolicyRuntime.Default();
        }
        else
        {
            index = _entities.Length;
            _entities.Add(default);
            _alive.Add(1);
            _generations.Add(1u);
            _chainPolicies.Add(ChainPolicyRuntime.Default());
            _piercePolicies.Add(PiercePolicyRuntime.Default());
            _expirationPolicies.Add(ExpirationPolicyRuntime.Default());
            _rehitPolicies.Add(RehitPolicyRuntime.Default());
            _frozenAppliers.Add(FrozenApplierRuntime.Default());
            _ignitedAppliers.Add(IgnitedApplierRuntime.Default());
            _shockedAppliers.Add(ShockedApplierRuntime.Default());
            _poisonedAppliers.Add(PoisonedApplierRuntime.Default());
            _stunnedAppliers.Add(StunnedApplierRuntime.Default());
            _bleedAppliers.Add(BleedApplierRuntime.Default());
            _motionPolicies.Add(MotionPolicyRuntime.Default());
        }

        _aliveCount++;
        return new EntityId { Index = index, Generation = _generations[index] };
    }
}
