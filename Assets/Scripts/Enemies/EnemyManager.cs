using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System;

public class EnemyManager
{
    private const int DefaultGridMaxEnemies = 10000;
    private const float unitLengthPerCell = 50f;

    private NativeList<EnemyMotion> _motion;
    private NativeList<EnemyVitality> _vitality;
    private NativeList<int> _entityIds;
    private NativeList<EnemyCombatTraits> _combatTraits;
    private NativeList<StatusAilmentFlag> _status;
    private NativeList<EnemyPresentation> _presentation;
    private int _nextEntityId;
    private GridSpatialPartition _grid;

    public EnemyManager(RectTransform simulationZone)
    {
        _motion = new NativeList<EnemyMotion>(Allocator.Persistent);
        _vitality = new NativeList<EnemyVitality>(Allocator.Persistent);
        _entityIds = new NativeList<int>(Allocator.Persistent);
        _combatTraits = new NativeList<EnemyCombatTraits>(Allocator.Persistent);
        _status = new NativeList<StatusAilmentFlag>(Allocator.Persistent);
        _presentation = new NativeList<EnemyPresentation>(Allocator.Persistent);
        _nextEntityId = 0;
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
            authoringData.AppendRuntimeColumns(
                pos,
                _nextEntityId++,
                (uint)randomSeed + (uint)i,
                _motion,
                _vitality,
                _entityIds,
                _combatTraits,
                _status,
                _presentation);
        }
    }

    /// <summary>
    /// Applies removals using <paramref name="indices"/> sorted ascending (paired with <paramref name="entityIds"/>).
    /// Removes from the highest index downward so swap-back stays valid. Skips stale rows when entity id mismatches.
    /// </summary>
    public void ApplyAscendingRemovalTrack(NativeList<int> indices, NativeList<int> entityIds)
    {
        UnityEngine.Assertions.Assert.AreEqual(indices.Length, entityIds.Length);
        if (indices.Length == 0)
            return;
        for (int i = indices.Length - 1; i >= 0; i--)
        {
            int idx = indices[i];
            int expectedId = entityIds[i];
            if (idx < 0 || idx >= _motion.Length)
                continue;
            if (_entityIds[idx] != expectedId)
                continue;
            _motion.RemoveAtSwapBack(idx);
            _vitality.RemoveAtSwapBack(idx);
            _entityIds.RemoveAtSwapBack(idx);
            _combatTraits.RemoveAtSwapBack(idx);
            _status.RemoveAtSwapBack(idx);
            _presentation.RemoveAtSwapBack(idx);
        }
    }

    /// <summary>
    /// Parallel column views. Valid until next list modification.
    /// </summary>
    public EnemyBuffers GetBuffers()
    {
        return new EnemyBuffers(
            _motion.AsArray(),
            _vitality.AsArray(),
            _entityIds.AsArray(),
            _combatTraits.AsArray(),
            _status.AsArray(),
            _presentation.AsArray());
    }

    /// <summary>
    /// Removes all enemies without disposing the underlying lists.
    /// </summary>
    public void Clear()
    {
        _motion.Clear();
        _vitality.Clear();
        _entityIds.Clear();
        _combatTraits.Clear();
        _status.Clear();
        _presentation.Clear();
    }

    public int EnemyCount => _motion.Length;

    public GridSpatialPartition Grid => _grid;

    /// <summary>
    /// Rebuilds the spatial grid from current enemy positions. Call once per frame before QueryEnemies.
    /// </summary>
    public void BuildGrid()
    {
        if (_grid != null && _motion.IsCreated)
        {
            NativeArray<EnemyMotion> motion = _motion.AsArray();
            _grid.BuildFromPositions(motion);
        }
    }

    /// <summary>
    /// Validates that the grid was built for the current enemy count.
    /// </summary>
    public void ValidateGridForCurrentEnemies()
    {
        if (_grid == null) return;
        int current = _motion.Length;
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
        if (_entityIds.IsCreated) _entityIds.Dispose();
        if (_combatTraits.IsCreated) _combatTraits.Dispose();
        if (_status.IsCreated) _status.Dispose();
        if (_presentation.IsCreated) _presentation.Dispose();
        _grid?.Dispose();
    }
}
