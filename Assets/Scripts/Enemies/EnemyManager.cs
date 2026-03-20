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

    private NativeList<Enemy> _enemies;
    private int _nextEntityId;
    private GridSpatialPartition _grid;

    public EnemyManager(RectTransform simulationZone)
    {
        _enemies = new NativeList<Enemy>(Allocator.Persistent);
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
            _enemies.Add(authoringData.CreateRuntimeEnemy(pos, _nextEntityId++, (uint)randomSeed + (uint)i));
        }
    }

    public void RemoveEnemies(List<int> enemyIds)
    {
        if (enemyIds == null || enemyIds.Count == 0) return;
        for (int i = _enemies.Length - 1; i >= 0 && enemyIds.Count > 0; i--)
        {
            int id = _enemies[i].entityId;
            int idx = enemyIds.IndexOf(id);
            if (idx >= 0)
            {
                _enemies.RemoveAtSwapBack(i);
                enemyIds.RemoveAt(idx);
            }
        }
    }

    /// <summary>
    /// Returns a read-write view of the enemy list. Valid until next list modification.
    /// After BuildGrid(), enemies are ordered by spatial cell for cache-friendly iteration of QueryEnemies results.
    /// </summary>
    public NativeArray<Enemy> GetEnemies()
    {
        return _enemies.AsArray();
    }

    /// <summary>
    /// Removes all enemies without disposing the underlying list.
    /// </summary>
    public void Clear()
    {
        _enemies.Clear();
    }

    public int EnemyCount => _enemies.Length;

    public GridSpatialPartition Grid => _grid;

    /// <summary>
    /// Rebuilds the spatial grid from current enemy positions. Call once per frame (or when needed) before QueryEnemies.
    /// </summary>
    public void BuildGrid()
    {
        if (_grid != null && _enemies.IsCreated && _enemies.Length > 0)
            _grid.Build(_enemies.AsArray());
    }

    /// <summary>
    /// Validates that the grid was built for the current enemy count. Call before using grid for collision/chain queries.
    /// Throws if grid is stale (BuildGrid not called this frame or enemy list changed).
    /// </summary>
    public void ValidateGridForCurrentEnemies()
    {
        if (_grid == null) return;
        int current = _enemies.Length;
        if (current > 0 && _grid.LastBuildEnemyCount != current)
            throw new InvalidOperationException($"EnemyManager: grid was built for {_grid.LastBuildEnemyCount} enemies but current count is {current}. Call BuildGrid() before collision/chain steps.");
    }

    /// <summary>
    /// Fills results with indices into the enemy array for enemies in cells overlapping the radius around pos.
    /// Does not filter by distance; caller may do a distance check. Call BuildGrid() first for current data.
    /// </summary>
    public void QueryEnemies(float2 pos, float radius, NativeList<int> results)
    {
        if (_grid == null) return;
        _grid.QueryNeighbors(pos, radius, results);
    }

    public void Dispose()
    {
        if (_enemies.IsCreated)
            _enemies.Dispose();
        _grid?.Dispose();
    }
}
