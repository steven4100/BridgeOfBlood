using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns enemies along the left edge of a rect; returns positions with x=0, y in [0, spawnLineLength].
/// Caller adds rect.xMin and rect.yMin to get world positions.
/// </summary>
public class EnemySpawner
{
    private readonly float _spawnRate;
    private readonly float _spawnLineLength;
    private int _lastTotalSpawns;

    public EnemySpawner(float spawnRate, float spawnLineLength)
    {
        _spawnRate = Mathf.Max(0.001f, spawnRate);
        _spawnLineLength = spawnLineLength;
    }

    /// <summary>
    /// Returns new spawn positions for this frame. Each position is (0, y) with y in [0, spawnLineLength].
    /// Add rect.xMin to x and rect.yMin to y to get rect-space coordinates.
    /// </summary>
    public List<Vector2> GetSpawnPositions(float time)
    {
        int total = Mathf.FloorToInt(time * _spawnRate);
        int count = Mathf.Max(0, total - _lastTotalSpawns);
        _lastTotalSpawns = total;

        var list = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
            list.Add(new Vector2(0f, Random.Range(0f, _spawnLineLength)));
        return list;
    }
}
