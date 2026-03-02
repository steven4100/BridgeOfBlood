using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Uniform grid spatial partition for fast nearby-enemy queries.
/// Build() reorders the enemy array so enemies in the same cell are contiguous (better cache locality).
/// Rebuild with Build() each frame (or when needed). Burst/job-friendly; NativeArrays only.
/// </summary>
public class GridSpatialPartition : IDebugDrawable
{
    private readonly float2 _boundsMin;
    private readonly float2 _boundsMax;
    private readonly float _cellSize;
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    private readonly int _totalCells;
    private readonly int _maxEnemyCount;

    private NativeArray<int> _cellCounts;
    private NativeArray<int> _cellStarts;
    private NativeArray<int> _writeOffsets;
    private NativeArray<Enemy> _tempEnemies;
    private int _lastBuildEnemyCount;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int TotalCells => _totalCells;
    /// <summary>Enemy count passed to last Build(). Use to validate grid matches current enemies before querying.</summary>
    public int LastBuildEnemyCount => _lastBuildEnemyCount;

    /// <summary>
    /// Allocates the grid and a persistent temp buffer for reordering. No allocations in Build().
    /// </summary>
    /// <param name="boundsMin">World bounds minimum (e.g. left-bottom).</param>
    /// <param name="boundsMax">World bounds maximum (e.g. right-top).</param>
    /// <param name="cellSize">Side length of each square cell.</param>
    /// <param name="maxEnemyCount">Max enemies to support (sizes temp buffer).</param>
    public GridSpatialPartition(float2 boundsMin, float2 boundsMax, float cellSize, int maxEnemyCount)
    {
        _boundsMin = boundsMin;
        _boundsMax = boundsMax;
        _cellSize = math.max(0.001f, cellSize);
        _maxEnemyCount = math.max(1, maxEnemyCount);

        float spanX = boundsMax.x - boundsMin.x;
        float spanY = boundsMax.y - boundsMin.y;
        _gridWidth = math.max(1, (int)math.ceil(spanX / _cellSize));
        _gridHeight = math.max(1, (int)math.ceil(spanY / _cellSize));
        _totalCells = _gridWidth * _gridHeight;

        _cellCounts = new NativeArray<int>(_totalCells, Allocator.Persistent);
        _cellStarts = new NativeArray<int>(_totalCells + 1, Allocator.Persistent);
        _writeOffsets = new NativeArray<int>(_totalCells + 1, Allocator.Persistent);
        _tempEnemies = new NativeArray<Enemy>(_maxEnemyCount, Allocator.Persistent);
    }

    /// <summary>
    /// Clamps position to bounds and returns the cell index. Returns -1 if outside bounds.
    /// </summary>
    public int GetCellIndex(float2 position)
    {
        if (position.x < _boundsMin.x || position.x >= _boundsMax.x ||
            position.y < _boundsMin.y || position.y >= _boundsMax.y)
            return -1;

        int cx = (int)((position.x - _boundsMin.x) / _cellSize);
        int cy = (int)((position.y - _boundsMin.y) / _cellSize);
        cx = math.clamp(cx, 0, _gridWidth - 1);
        cy = math.clamp(cy, 0, _gridHeight - 1);
        return cy * _gridWidth + cx;
    }

    /// <summary>
    /// Rebuilds the grid and reorders the enemy array so enemies in the same cell are contiguous.
    /// Enemies outside bounds are clamped into edge cells. No allocations inside.
    /// After Build(), enemies[cellStarts[i] .. cellStarts[i+1]) are the enemies in cell i.
    /// </summary>
    public void Build(NativeArray<Enemy> enemies)
    {
        int N = math.min(enemies.Length, _maxEnemyCount);
        _lastBuildEnemyCount = N;
        if (N == 0) return;

        for (int i = 0; i < _totalCells; i++)
            _cellCounts[i] = 0;

        for (int i = 0; i < N; i++)
        {
            int cell = GetCellIndexClamped(enemies[i].position);
            _cellCounts[cell]++;
        }

        _cellStarts[0] = 0;
        for (int i = 0; i < _totalCells; i++)
            _cellStarts[i + 1] = _cellStarts[i] + _cellCounts[i];

        for (int i = 0; i <= _totalCells; i++)
            _writeOffsets[i] = _cellStarts[i];

        for (int i = 0; i < N; i++)
        {
            int cell = GetCellIndexClamped(enemies[i].position);
            int dest = _writeOffsets[cell];
            _tempEnemies[dest] = enemies[i];
            _writeOffsets[cell]++;
        }

        for (int i = 0; i < N; i++)
            enemies[i] = _tempEnemies[i];
    }

    /// <summary>
    /// Fills results with indices into the (reordered) enemy array for cells overlapping the radius.
    /// Indices are contiguous per cell for cache-friendly iteration. No distance check; caller may filter.
    /// </summary>
    public void QueryNeighbors(float2 position, float radius, NativeList<int> results)
    {
        float xMin = position.x - radius;
        float xMax = position.x + radius;
        float yMin = position.y - radius;
        float yMax = position.y + radius;

        int cellMinX = (int)((xMin - _boundsMin.x) / _cellSize);
        int cellMaxX = (int)((xMax - _boundsMin.x) / _cellSize);
        int cellMinY = (int)((yMin - _boundsMin.y) / _cellSize);
        int cellMaxY = (int)((yMax - _boundsMin.y) / _cellSize);

        cellMinX = math.clamp(cellMinX, 0, _gridWidth - 1);
        cellMaxX = math.clamp(cellMaxX, 0, _gridWidth - 1);
        cellMinY = math.clamp(cellMinY, 0, _gridHeight - 1);
        cellMaxY = math.clamp(cellMaxY, 0, _gridHeight - 1);

        for (int cy = cellMinY; cy <= cellMaxY; cy++)
        {
            for (int cx = cellMinX; cx <= cellMaxX; cx++)
            {
                int cellIndex = cy * _gridWidth + cx;
                int start = _cellStarts[cellIndex];
                int end = _cellStarts[cellIndex + 1];
                for (int j = start; j < end; j++)
                    results.Add(j);
            }
        }
    }

    public void Dispose()
    {
        if (_cellCounts.IsCreated) _cellCounts.Dispose();
        if (_cellStarts.IsCreated) _cellStarts.Dispose();
        if (_writeOffsets.IsCreated) _writeOffsets.Dispose();
        if (_tempEnemies.IsCreated) _tempEnemies.Dispose();
    }

    public void DrawGizmos(Transform transform)
    {
        if (transform == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        for (int cy = 0; cy <= _gridHeight; cy++)
        {
            float y = _boundsMin.y + cy * _cellSize;
            Vector3 a = transform.TransformPoint(new Vector3(_boundsMin.x, y, 0f));
            Vector3 b = transform.TransformPoint(new Vector3(_boundsMax.x, y, 0f));
            Gizmos.DrawLine(a, b);
        }
        for (int cx = 0; cx <= _gridWidth; cx++)
        {
            float x = _boundsMin.x + cx * _cellSize;
            Vector3 a = transform.TransformPoint(new Vector3(x, _boundsMin.y, 0f));
            Vector3 b = transform.TransformPoint(new Vector3(x, _boundsMax.y, 0f));
            Gizmos.DrawLine(a, b);
        }

#if UNITY_EDITOR
        if (!_cellCounts.IsCreated) return;
        for (int cy = 0; cy < _gridHeight; cy++)
        {
            for (int cx = 0; cx < _gridWidth; cx++)
            {
                int cellIndex = cy * _gridWidth + cx;
                int count = _cellCounts[cellIndex];
                float centerX = _boundsMin.x + (cx + 0.5f) * _cellSize;
                float centerY = _boundsMin.y + (cy + 0.5f) * _cellSize;
                Vector3 worldCenter = transform.TransformPoint(new Vector3(centerX, centerY, 0f));
                Handles.Label(worldCenter, count.ToString());
            }
        }
#endif
    }

    private int GetCellIndexClamped(float2 position)
    {
        float x = math.clamp(position.x, _boundsMin.x, _boundsMax.x - 0.0001f);
        float y = math.clamp(position.y, _boundsMin.y, _boundsMax.y - 0.0001f);
        int cx = (int)((x - _boundsMin.x) / _cellSize);
        int cy = (int)((y - _boundsMin.y) / _cellSize);
        cx = math.clamp(cx, 0, _gridWidth - 1);
        cy = math.clamp(cy, 0, _gridHeight - 1);
        return cy * _gridWidth + cx;
    }
}
