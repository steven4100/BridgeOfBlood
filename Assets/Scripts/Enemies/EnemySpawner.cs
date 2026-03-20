using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces spawn event origins along the left edge of a rect. Returns one origin per event: (0, y) in local line space.
/// Caller adds rect.xMin and rect.yMin to get world origin, then applies the spawn pattern to get positions.
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
    /// Returns spawn event origins for this frame. Each origin is (0, y) with y in [0, spawnLineLength].
    /// Add rect.xMin to x and rect.yMin to y to get world origin; then use SpawnPattern.GetPositions(origin, ...) for positions.
    /// </summary>
    /// <summary>
    /// Resets spawn tracking so next GetSpawnEventOrigins starts fresh (e.g. on new round).
    /// </summary>
    public void Reset()
    {
        _lastTotalSpawns = 0;
    }

    public List<Vector2> GetSpawnEventOrigins(float time)
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
