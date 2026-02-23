using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime state of a live attack entity (projectile, AoE, etc.).
/// Spawned from AttackEntityData when a spell keyframe fires.
/// </summary>
public struct AttackEntity
{
    public int entityId;
    public float2 position;
    public float2 velocity;
    public float timeAlive;
    public float distanceTravelled;
    public int enemiesHit;

    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public AttackEntityLifecycleData lifecycle;
    public HitBoxData hitBox;
    public float currentHitBoxScale;
}

/// <summary>
/// Manages live attack entities. Spawns from AttackEntityData, ticks lifetime/distance,
/// and removes expired entities. NativeList-backed, no per-entity MonoBehaviours.
/// </summary>
public class AttackEntityManager
{
    private NativeList<AttackEntity> _entities;
    private int _nextEntityId;

    public AttackEntityManager()
    {
        _entities = new NativeList<AttackEntity>(Allocator.Persistent);
        _nextEntityId = 0;
    }

    /// <summary>
    /// Spawns a new attack entity from the given data at the specified position.
    /// </summary>
    public int Spawn(AttackEntityData data, float2 spawnPosition)

    {
        int id = _nextEntityId++;
        _entities.Add(new AttackEntity
        {
            entityId = id,
            position = spawnPosition,
            velocity = data.entityVelocity,
            timeAlive = 0f,
            distanceTravelled = 0f,
            enemiesHit = 0,
            physicalDamage = data.physicalDamage,
            coldDamage = data.coldDamage,
            fireDamage = data.fireDamage,
            lightningDamage = data.lightningDamage,
            lifecycle = data.lifecycleData,
            hitBox = data.hitBoxData,
            currentHitBoxScale = 1f
        });
        return id;
    }

    /// <summary>
    /// Returns a read-write view of the entity list. Valid until next list modification.
    /// </summary>
    public NativeArray<AttackEntity> GetEntities()
    {
        return _entities.AsArray();
    }

    public int EntityCount => _entities.Length;

    /// <summary>
    /// Removes entities that have exceeded their lifecycle limits (time, distance, or max hits).
    /// Call after movement and hit detection each frame.
    /// </summary>
    public void RemoveExpired()
    {
        for (int i = _entities.Length - 1; i >= 0; i--)
        {
            var e = _entities[i];
            bool expired = false;

            if (e.lifecycle.maxTimeAlive > 0f && e.timeAlive >= e.lifecycle.maxTimeAlive)
                expired = true;
            if (e.lifecycle.maxDistanceTravelled > 0f && e.distanceTravelled >= e.lifecycle.maxDistanceTravelled)
                expired = true;
            if (e.lifecycle.maxNumEnemiesHit > 0 && e.enemiesHit >= e.lifecycle.maxNumEnemiesHit)
                expired = true;

            if (expired)
                _entities.RemoveAtSwapBack(i);
        }
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
    }

    public void Dispose()
    {
        if (_entities.IsCreated)
            _entities.Dispose();
    }
}
