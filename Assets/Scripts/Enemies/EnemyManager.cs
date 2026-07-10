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
    private NativeList<uint> _generations;
    private NativeList<byte> _alive;
    private NativeList<int> _freeSlots;
    private NativeList<EnemyCombatTraits> _combatTraits;
    private NativeList<StatusAilmentFlag> _status;
    private NativeList<EnemyPresentation> _presentation;
    private int _aliveCount;
    private GridSpatialPartition _grid;

    public EnemyManager(RectTransform simulationZone)
    {
        _motion = new NativeList<EnemyMotion>(Allocator.Persistent);
        _vitality = new NativeList<EnemyVitality>(Allocator.Persistent);
        _generations = new NativeList<uint>(Allocator.Persistent);
        _alive = new NativeList<byte>(Allocator.Persistent);
        _freeSlots = new NativeList<int>(Allocator.Persistent);
        _combatTraits = new NativeList<EnemyCombatTraits>(Allocator.Persistent);
        _status = new NativeList<StatusAilmentFlag>(Allocator.Persistent);
        _presentation = new NativeList<EnemyPresentation>(Allocator.Persistent);
        _aliveCount = 0;
        _grid = new GridSpatialPartition(
            new float2(simulationZone.rect.xMin, simulationZone.rect.yMin),
            new float2(simulationZone.rect.xMax, simulationZone.rect.yMax),
            unitLengthPerCell,
            DefaultGridMaxEnemies);
    }

    /// <summary>
    /// Spawns one enemy per position using the given authoring data.
    /// </summary>
    public void CreateEnemies(List<Vector2> positions, EnemyAuthoringData authoringData)
    {
        int randomSeed = UnityEngine.Random.Range(0, 1000);
        if (positions == null || authoringData == null) return;
        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            var pos = new float2(p.x, p.y);
            EntityId id = AllocateSlot();
            authoringData.WriteRuntimeColumnsAt(
                id.Index,
                pos,
                (uint)randomSeed + (uint)i,
                _motion,
                _vitality,
                _combatTraits,
                _status,
                _presentation);
        }
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

        _alive[id.Index] = 0;
        _status[id.Index] = 0;
        _freeSlots.Add(id.Index);
        _aliveCount--;
    }

    public bool IsValid(EntityId id) =>
        id.Index >= 0
        && id.Index < _generations.Length
        && _alive[id.Index] != 0
        && _generations[id.Index] == id.Generation;

    /// <summary>
    /// Parallel column views. Valid until next list modification.
    /// </summary>
    public EnemyBuffers GetBuffers()
    {
        return new EnemyBuffers(
            _motion.AsArray(),
            _vitality.AsArray(),
            _generations.AsArray(),
            _alive.AsArray(),
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
        _generations.Clear();
        _alive.Clear();
        _freeSlots.Clear();
        _combatTraits.Clear();
        _status.Clear();
        _presentation.Clear();
        _aliveCount = 0;
    }

    public int EnemyCount => _aliveCount;
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
            _grid.BuildFromPositions(motion, _alive.AsArray(), _aliveCount);
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

    public void Dispose()
    {
        if (_motion.IsCreated) _motion.Dispose();
        if (_vitality.IsCreated) _vitality.Dispose();
        if (_generations.IsCreated) _generations.Dispose();
        if (_alive.IsCreated) _alive.Dispose();
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

            uint generation = _generations[index] + 1u;
            _generations[index] = generation != 0u ? generation : 1u;
            _alive[index] = 1;
        }
        else
        {
            index = _motion.Length;
            _motion.Add(default);
            _vitality.Add(default);
            _generations.Add(1u);
            _alive.Add(1);
            _combatTraits.Add(default);
            _status.Add(default);
            _presentation.Add(default);
        }

        _aliveCount++;
        return new EntityId { Index = index, Generation = _generations[index] };
    }
}
