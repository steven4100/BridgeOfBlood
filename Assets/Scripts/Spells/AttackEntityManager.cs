using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Runtime state of a live attack entity (projectile, AoE, etc.).
/// Spawned from an AttackEntityBuildContext when a spell keyframe fires.
/// </summary>
public struct AttackEntity
{
    public int entityId;
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
/// NativeList-backed, no per-entity MonoBehaviours.
/// Call ValidateParallelLists / ValidateHitEvents (code owner) before passing data to systems; systems assume valid input.
/// </summary>
public class AttackEntityManager
{
    private NativeList<AttackEntity> _entities;
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
    private int _nextEntityId;

    /// <summary>
    /// Hit-conditional modifier sets, keyed by entity id. Snapshotted from <see cref="SpellModifications.attackEntityModifiers"/>
    /// at spawn so live entities carry the conditionals that were active when they were created. Read by
    /// <see cref="DamageSystem"/> via <see cref="HitModifierSets"/> at hit time. Keyed by id (stable across swap-back removal).
    /// </summary>
    private readonly Dictionary<int, List<AttackEntityModifier>> _hitModifierSets = new Dictionary<int, List<AttackEntityModifier>>();

    public AttackEntityManager()
    {
        _entities = new NativeList<AttackEntity>(Allocator.Persistent);
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
        _nextEntityId = 0;
    }

    /// <summary>
    /// Read-only view of hit-conditional modifier sets keyed by entity id. Consumed by <see cref="DamageSystem"/> at hit time.
    /// </summary>
    public IReadOnlyDictionary<int, List<AttackEntityModifier>> HitModifierSets => _hitModifierSets;

    /// <summary>
    /// Spawns a new attack entity from <paramref name="ctx"/>. Rolls stats + applies spell modifications via
    /// <see cref="AttackEntityModificationApplicator"/>, appends default policies, then lets each authoring
    /// behavior write its contribution into the parallel lists by index. No intermediate payload struct.
    /// </summary>
    public int Spawn(in AttackEntityBuildContext ctx)
    {
        int id = _nextEntityId++;
        int idx = _entities.Length;

        AttackEntity entity = AttackEntityModificationApplicator.BuildRolledEntity(in ctx, id);
        if (ctx.eventScaledDamage > 0f)
            AttackEntityModificationApplicator.ApplyEventScaledDamage(ref entity, ctx.eventScaledDamage);

        _entities.Add(entity);
        _chainPolicies.Add(ChainPolicyRuntime.Default());
        _piercePolicies.Add(PiercePolicyRuntime.Default());
        _expirationPolicies.Add(ExpirationPolicyRuntime.Default());

        var rehit = RehitPolicyRuntime.Default();
        rehit.rehitCooldownSeconds = ctx.data.rehitCooldownSeconds;
        _rehitPolicies.Add(rehit);

        _frozenAppliers.Add(FrozenApplierRuntime.Default());
        _ignitedAppliers.Add(IgnitedApplierRuntime.Default());
        _shockedAppliers.Add(ShockedApplierRuntime.Default());
        _poisonedAppliers.Add(PoisonedApplierRuntime.Default());
        _stunnedAppliers.Add(StunnedApplierRuntime.Default());
        _bleedAppliers.Add(BleedApplierRuntime.Default());

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
    /// Returns a read-write view of the entity list. Valid until next list modification.
    /// </summary>
    public NativeArray<AttackEntity> GetEntities()
    {
        return _entities.AsArray();
    }

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

    public int EntityCount => _entities.Length;

    /// <summary>
    /// Validates that entity and policy lists have matching lengths. Call before passing arrays to systems.
    /// Throws if inconsistent (indicates internal bug).
    /// </summary>
    public void ValidateParallelLists()
    {
        int n = _entities.Length;
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
    /// Validates that all hit events reference valid attack-entity and enemy indices. Call before passing hitEvents to ChainSystem/DamageSystem.
    /// Throws if any index is out of range (indicates upstream bug).
    /// </summary>
    public void ValidateHitEvents(NativeArray<HitEvent>.ReadOnly hitEvents, int enemySlotCount)
    {
        int entityCount = _entities.Length;
        int chainCount = _chainPolicies.Length;
        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];
            if (hit.attackEntityIndex < 0 || hit.attackEntityIndex >= entityCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].attackEntityIndex={hit.attackEntityIndex} is out of range [0, {entityCount}).");
            if (hit.attackEntityIndex >= chainCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].attackEntityIndex={hit.attackEntityIndex} is out of range for chainPolicies.Length={chainCount}.");
            if (hit.enemyIndex < 0 || hit.enemyIndex >= enemySlotCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].enemyIndex={hit.enemyIndex} is out of range [0, {enemySlotCount}).");
        }
    }

    /// <summary>
    /// Applies pending removal events: removes each listed entity from all policy lists.
    /// Call at end of simulation after pierce/expiration (and any other) systems have appended to the list.
    /// Does not clear the list; caller should clear after.
    /// </summary>
    public void ApplyRemovals(NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        for (int i = 0; i < removalEvents.Length; i++)
            RemoveEntity(removalEvents[i].entityId);
    }

    /// <summary>
    /// Removes a single entity by id.
    /// </summary>
    public void RemoveEntity(int entityId)
    {
        for (int i = _entities.Length - 1; i >= 0; i--)
        {
            if (_entities[i].entityId == entityId)
            {
                _hitModifierSets.Remove(entityId);
                _entities.RemoveAtSwapBack(i);
                _chainPolicies.RemoveAtSwapBack(i);
                _piercePolicies.RemoveAtSwapBack(i);
                _expirationPolicies.RemoveAtSwapBack(i);
                _rehitPolicies.RemoveAtSwapBack(i);
                _frozenAppliers.RemoveAtSwapBack(i);
                _ignitedAppliers.RemoveAtSwapBack(i);
                _shockedAppliers.RemoveAtSwapBack(i);
                _poisonedAppliers.RemoveAtSwapBack(i);
                _stunnedAppliers.RemoveAtSwapBack(i);
                _bleedAppliers.RemoveAtSwapBack(i);
                return;
            }
        }
    }

    /// <summary>
    /// Removes all attack entities.
    /// </summary>
    public void Clear()
    {
        _hitModifierSets.Clear();
        _entities.Clear();
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
    }

    public void Dispose()
    {
        if (_entities.IsCreated) _entities.Dispose();
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
    }
}
