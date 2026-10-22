using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

public class EnemyManager
{
    private const int DefaultGridMaxEnemies = 10000;
    private const float unitLengthPerCell = 50f;

    private NativeList<EnemyMotion> _motion;
    private NativeList<EnemyVitality> _vitality;
    private NativeList<EnemyId> _ids;
    private NativeList<EnemyHurtbox> _hurtboxes;
    private NativeList<int> _freeSlots;
    private NativeList<EnemyCombatTraits> _combatTraits;
    private NativeList<StatusAilmentFlag> _status;
    private NativeList<EnemyPresentation> _presentation;
    private int _aliveCount;
    private int _spawnedThisRound;
    private GridSpatialPartition _grid;

    public EnemyManager(Rect playfield)
    {
        _motion = new NativeList<EnemyMotion>(Allocator.Persistent);
        _vitality = new NativeList<EnemyVitality>(Allocator.Persistent);
        _ids = new NativeList<EnemyId>(Allocator.Persistent);
        _hurtboxes = new NativeList<EnemyHurtbox>(Allocator.Persistent);
        _freeSlots = new NativeList<int>(Allocator.Persistent);
        _combatTraits = new NativeList<EnemyCombatTraits>(Allocator.Persistent);
        _status = new NativeList<StatusAilmentFlag>(Allocator.Persistent);
        _presentation = new NativeList<EnemyPresentation>(Allocator.Persistent);
        _aliveCount = 0;
        _grid = CreateGrid(playfield);
    }

    /// <summary>
    /// Rebuilds the spatial partition for a new playfield. Call when applying a round config, before spawn.
    /// </summary>
    public void RebuildSpatialGrid(Rect playfield)
    {
        _grid?.Dispose();
        _grid = CreateGrid(playfield);
    }

    static GridSpatialPartition CreateGrid(Rect playfield)
    {
        return new GridSpatialPartition(
            new float2(playfield.xMin, playfield.yMin),
            new float2(playfield.xMax, playfield.yMax),
            unitLengthPerCell,
            DefaultGridMaxEnemies);
    }

    /// <summary>
    /// Spawns one enemy per position using the given authoring data.
    /// </summary>
    public void CreateEnemies(
        List<Vector2> positions,
        EnemyAuthoringData authoringData,
        float healthMultiplier,
        float moveSpeedMultiplier)
    {
        int randomSeed = UnityEngine.Random.Range(0, 1000);
        if (positions == null || authoringData == null) return;
        _spawnedThisRound += positions.Count;
        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            var pos = new float2(p.x, p.y);
            EntityId id = AllocateSlot();
            authoringData.WriteRuntimeColumnsAt(
                id.Index,
                pos,
                (uint)randomSeed + (uint)i,
                healthMultiplier,
                moveSpeedMultiplier,
                _motion,
                _hurtboxes,
                _vitality,
                _combatTraits,
                _status,
                _presentation);
        }
    }

    /// <summary>
    /// Spawns one permanent map object. Does not increment <see cref="SpawnedThisRound"/>.
    /// </summary>
    public EntityId CreateMapObject(float2 position, float collisionRadius, EntityVisual visual)
    {
        EntityId id = AllocateSlot();
        _motion[id.Index] = new EnemyMotion
        {
            position = position,
            moveSpeed = 0f,
            knockbackVelocity = float2.zero
        };
        _hurtboxes[id.Index] = new EnemyHurtbox { radius = collisionRadius };
        EnemyId slot = _ids[id.Index];
        slot.IsMapObject = 1;
        _ids[id.Index] = slot;
        _vitality[id.Index] = new EnemyVitality { health = 1f, maxHealth = 1f };
        _combatTraits[id.Index] = default;
        _status[id.Index] = 0;
        _presentation[id.Index] = new EnemyPresentation
        {
            visual = visual,
            onDeathSound = AudioUnitRuntime.None,
            visualTime = 0f,
            ailmentFlashTimer = 0f,
            ailmentFlashSource = default
        };
        return id;
    }

    /// <summary>
    /// Applies removals by stable id. Removing a live slot leaves a tombstone and never moves later slots.
    /// </summary>
    public void ApplyRemovals(NativeList<EntityId> entityIds)
    {
        for (int i = 0; i < entityIds.Length; i++)
            Remove(entityIds[i]);
    }

    public void Remove(EntityId id)
    {
        if (!IsValid(id))
            return;

        EnemyId slot = _ids[id.Index];
        slot.IsAlive = 0;
        _ids[id.Index] = slot;
        _status[id.Index] = 0;
        _freeSlots.Add(id.Index);
        _aliveCount--;
    }

    public bool IsValid(EntityId id) =>
        id.Index >= 0
        && id.Index < _ids.Length
        && _ids[id.Index].IsAlive != 0
        && _ids[id.Index].Generation == id.Generation;

    /// <summary>
    /// Parallel column views. Valid until next list modification.
    /// </summary>
    public EnemyBuffers GetBuffers()
    {
        return new EnemyBuffers(
            _motion.AsArray(),
            _vitality.AsArray(),
            _ids.AsArray(),
            _hurtboxes.AsArray(),
            _combatTraits.AsArray(),
            _status.AsArray(),
            _presentation.AsArray(),
            _aliveCount);
    }

    /// <summary>
    /// Removes all enemies without disposing the underlying lists.
    /// </summary>
    public void Clear()
    {
        _motion.Clear();
        _vitality.Clear();
        _ids.Clear();
        _hurtboxes.Clear();
        _freeSlots.Clear();
        _combatTraits.Clear();
        _status.Clear();
        _presentation.Clear();
        _aliveCount = 0;
        _spawnedThisRound = 0;
    }

    public int EnemyCount => _aliveCount;
    public int SpawnedThisRound => _spawnedThisRound;
    public int SlotCount => _motion.Length;

    public GridSpatialPartition Grid => _grid;

    /// <summary>
    /// Rebuilds the spatial grid from current enemy positions. Call once per frame before QueryEnemies.
    /// </summary>
    public void BuildGrid()
    {
        if (_grid != null && _motion.IsCreated)
        {
            NativeArray<EnemyMotion> motion = _motion.AsArray();
            _grid.BuildFromPositions(motion, _ids.AsArray(), _aliveCount);
        }
    }

    /// <summary>
    /// Validates that the grid was built for the current enemy count.
    /// </summary>
    public void ValidateGridForCurrentEnemies()
    {
        if (_grid == null) return;
        int current = _aliveCount;
        if (current > 0 && _grid.LastBuildEnemyCount != current)
            throw new InvalidOperationException($"EnemyManager: grid was built for {_grid.LastBuildEnemyCount} enemies but current count is {current}. Call BuildGrid() before collision/chain steps.");
    }

    /// <summary>
    /// Fills results with enemy indices for enemies in cells overlapping the radius around pos.
    /// </summary>
    public void QueryEnemies(float2 pos, float radius, NativeList<int> results)
    {
        if (_grid == null) return;
        _grid.QueryNeighbors(pos, radius, results);
    }

    /// <summary>
    /// True when any live enemy circle overlaps the circle at <paramref name="origin"/>.
    /// Rebuilds the spatial grid from current positions first.
    /// </summary>
    public bool FootprintOverlapsEnemy(float2 origin, float radius)
    {
        BuildGrid();
        if (_grid == null || radius < 0f)
            return false;

        float queryRadius = radius + ComputeMaxHurtboxRadius();
        if (queryRadius <= 0f)
            return false;

        using var candidates = new NativeList<int>(Allocator.Temp);
        QueryEnemies(origin, queryRadius, candidates);
        for (int i = 0; i < candidates.Length; i++)
        {
            int enemyIndex = candidates[i];
            if (_ids[enemyIndex].IsAlive == 0)
                continue;

            float reach = radius + _hurtboxes[enemyIndex].radius;
            if (math.distancesq(origin, _motion[enemyIndex].position) < reach * reach)
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_motion.IsCreated) _motion.Dispose();
        if (_vitality.IsCreated) _vitality.Dispose();
        if (_ids.IsCreated) _ids.Dispose();
        if (_hurtboxes.IsCreated) _hurtboxes.Dispose();
        if (_freeSlots.IsCreated) _freeSlots.Dispose();
        if (_combatTraits.IsCreated) _combatTraits.Dispose();
        if (_status.IsCreated) _status.Dispose();
        if (_presentation.IsCreated) _presentation.Dispose();
        _grid?.Dispose();
    }

    private EntityId AllocateSlot()
    {
        int index;
        if (_freeSlots.Length > 0)
        {
            int last = _freeSlots.Length - 1;
            index = _freeSlots[last];
            _freeSlots.RemoveAt(last);

            uint generation = _ids[index].Generation + 1u;
            _ids[index] = new EnemyId
            {
                Generation = generation != 0u ? generation : 1u,
                IsAlive = 1,
                IsMapObject = 0
            };
        }
        else
        {
            index = _motion.Length;
            _motion.Add(default);
            _vitality.Add(default);
            _ids.Add(new EnemyId { Generation = 1u, IsAlive = 1 });
            _hurtboxes.Add(default);
            _combatTraits.Add(default);
            _status.Add(default);
            _presentation.Add(default);
        }

        _aliveCount++;
        return new EntityId { Index = index, Generation = _ids[index].Generation };
    }

    /// <summary>Largest live hurtbox radius. Scanned when a query needs it.</summary>
    public float ComputeMaxHurtboxRadius()
    {
        float max = 0f;
        for (int i = 0; i < _ids.Length; i++)
        {
            if (_ids[i].IsAlive == 0)
                continue;
            float radius = _hurtboxes[i].radius;
            if (radius > max)
                max = radius;
        }

        return max;
    }
}
