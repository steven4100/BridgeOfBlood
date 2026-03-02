using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Runtime state of a live attack entity (projectile, AoE, etc.).
/// Spawned from AttackEntitySpawnPayload when a spell keyframe fires.
/// </summary>
public struct AttackEntity
{
    public int entityId;
    public float2 position;
    public float2 velocity;
    public float timeAlive;
    public float distanceTravelled;
    public int enemiesHit;
    public float rehitCooldownSeconds;

    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public HitBoxData hitBox;
    public float currentHitBoxScale;
}

/// <summary>
/// Manages live attack entities. Spawns from AttackEntitySpawnPayload (built from authoring);
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
    private int _nextEntityId;

    public AttackEntityManager()
    {
        _entities = new NativeList<AttackEntity>(Allocator.Persistent);
        _chainPolicies = new NativeList<ChainPolicyRuntime>(Allocator.Persistent);
        _piercePolicies = new NativeList<PiercePolicyRuntime>(Allocator.Persistent);
        _expirationPolicies = new NativeList<ExpirationPolicyRuntime>(Allocator.Persistent);
        _rehitPolicies = new NativeList<RehitPolicyRuntime>(Allocator.Persistent);
        _nextEntityId = 0;
    }

    /// <summary>
    /// Spawns a new attack entity from the given payload at the specified position.
    /// </summary>
    public int Spawn(AttackEntitySpawnPayload payload, float2 spawnPosition)
    {
        int id = _nextEntityId++;
        _entities.Add(new AttackEntity
        {
            entityId = id,
            position = spawnPosition,
            velocity = payload.velocity,
            timeAlive = 0f,
            distanceTravelled = 0f,
            enemiesHit = 0,
            rehitCooldownSeconds = payload.rehit.rehitCooldownSeconds,
            physicalDamage = payload.physicalDamage,
            coldDamage = payload.coldDamage,
            fireDamage = payload.fireDamage,
            lightningDamage = payload.lightningDamage,
            hitBox = payload.hitBoxData,
            currentHitBoxScale = 1f
        });
        _chainPolicies.Add(payload.chain);
        _piercePolicies.Add(payload.pierce);
        _expirationPolicies.Add(payload.expiration);
        _rehitPolicies.Add(payload.rehit);
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
    }

    /// <summary>
    /// Records each hit into the attack entity's rehit list (enemyId, timeAlive) so the next frame's resolver can reject rehits within cooldown.
    /// Call after DamageSystem.ProcessHits. Only updates entities with rehitCooldownSeconds > 0.
    /// </summary>
    public void RecordRehitHits(
        NativeList<HitEvent> hitEvents,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies)
    {
        NativeArray<RehitPolicyRuntime> rehitPolicies = _rehitPolicies.AsArray();
        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];
            int ai = hit.attackEntityIndex;
            if (ai < 0 || ai >= rehitPolicies.Length) continue;
            if (hit.enemyIndex < 0 || hit.enemyIndex >= enemies.Length) continue;

            RehitPolicyRuntime rehit = rehitPolicies[ai];
            if (rehit.rehitCooldownSeconds <= 0f) continue;

            AttackEntity atk = attackEntities[ai];
            Enemy enemy = enemies[hit.enemyIndex];
            var entry = new RehitEntry { enemyId = enemy.entityId, hitTimeAlive = atk.timeAlive };

            if (rehit.recentHits.Length >= rehit.recentHits.Capacity)
            {
                int oldestIndex = 0;
                float oldest = rehit.recentHits[0].hitTimeAlive;
                for (int j = 1; j < rehit.recentHits.Length; j++)
                {
                    if (rehit.recentHits[j].hitTimeAlive < oldest)
                    {
                        oldest = rehit.recentHits[j].hitTimeAlive;
                        oldestIndex = j;
                    }
                }
                rehit.recentHits.RemoveAt(oldestIndex);
            }
            rehit.recentHits.Add(entry);
            rehitPolicies[ai] = rehit;
        }
    }

    /// <summary>
    /// Validates that all hit events reference valid attack-entity and enemy indices. Call before passing hitEvents to ChainSystem/DamageSystem.
    /// Throws if any index is out of range (indicates upstream bug).
    /// </summary>
    public void ValidateHitEvents(NativeList<HitEvent> hitEvents, int enemyCount)
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
            if (hit.enemyIndex < 0 || hit.enemyIndex >= enemyCount)
                throw new ArgumentOutOfRangeException(nameof(hitEvents), $"HitEvent[{i}].enemyIndex={hit.enemyIndex} is out of range [0, {enemyCount}).");
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
                _entities.RemoveAtSwapBack(i);
                _chainPolicies.RemoveAtSwapBack(i);
                _piercePolicies.RemoveAtSwapBack(i);
                _expirationPolicies.RemoveAtSwapBack(i);
                _rehitPolicies.RemoveAtSwapBack(i);
                return;
            }
        }
    }

    /// <summary>
    /// Removes all attack entities.
    /// </summary>
    public void Clear()
    {
        _entities.Clear();
        _chainPolicies.Clear();
        _piercePolicies.Clear();
        _expirationPolicies.Clear();
        _rehitPolicies.Clear();
    }

    public void Dispose()
    {
        if (_entities.IsCreated)
            _entities.Dispose();
        if (_chainPolicies.IsCreated)
            _chainPolicies.Dispose();
        if (_piercePolicies.IsCreated)
            _piercePolicies.Dispose();
        if (_expirationPolicies.IsCreated)
            _expirationPolicies.Dispose();
        if (_rehitPolicies.IsCreated)
            _rehitPolicies.Dispose();
    }
}
