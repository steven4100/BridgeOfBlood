using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using UnityEngine;

/// <summary>
/// Rate-based edge spawner: emits resolved requests using owned <see cref="EnemySpawnTable"/> and <see cref="SpawnPattern"/> per pick.
/// Spawn Y spans the playfield rect passed into <see cref="CollectSpawnRequests"/>.
/// </summary>
[Serializable]
public class EnemySpawner : IEnemySpawner
{
    public EnemySpawnTable spawnTable;

    [Tooltip("Spawn events per second of simulation time.")]
    public float spawnRate = 2f;

    int _lastTotalSpawns;
    readonly List<Vector2> _positionsScratch = new List<Vector2>();

    public EnemySpawner() { }

    public EnemySpawner(float spawnRate)
    {
        this.spawnRate = spawnRate;
    }

    public void Reset()
    {
        _lastTotalSpawns = 0;
    }

    public List<EnemySpawnRequest> CollectSpawnRequests(float time, Rect playfield)
    {
        int total = Mathf.FloorToInt(time * spawnRate);
        int count = Mathf.Max(0, total - _lastTotalSpawns);
        _lastTotalSpawns = total;

        if (count == 0 || spawnTable == null)
            return new List<EnemySpawnRequest>();

        var requests = new List<EnemySpawnRequest>(count);
        uint baseSeed = (uint)(time * 1000f).GetHashCode();

        for (int i = 0; i < count; i++)
        {
            Vector2 origin = new Vector2(playfield.xMin, UnityEngine.Random.Range(playfield.yMin, playfield.yMax));
            AddResolvedRequest(origin, baseSeed + (uint)i, requests);
        }

        return requests;
    }

    void AddResolvedRequest(Vector2 origin, uint seed, List<EnemySpawnRequest> requestsOut)
    {
        if (!TryResolveAtOrigin(origin, seed, _positionsScratch, out EnemyAuthoringData enemy))
            return;

        requestsOut.Add(new EnemySpawnRequest
        {
            enemy = enemy,
            positions = new List<Vector2>(_positionsScratch)
        });
    }

    bool TryResolveAtOrigin(Vector2 origin, uint seed, List<Vector2> positionsOut, out EnemyAuthoringData enemy)
    {
        enemy = null;
        positionsOut.Clear();

        EnemySpawnPick pick = spawnTable.PickEnemyByWeight(seed);
        enemy = pick.enemy;
        if (enemy == null)
            return false;

        SpawnPattern pattern = pick.pattern != null ? pick.pattern : spawnTable.fallbackSpawnPattern;
        if (pattern != null)
            pattern.GetPositions(origin, positionsOut, seed + 1000u);
        else
            positionsOut.Add(origin);

        if (pick.positionScale != 1f && positionsOut.Count > 0)
        {
            for (int j = 0; j < positionsOut.Count; j++)
            {
                Vector2 p = positionsOut[j];
                positionsOut[j] = origin + (p - origin) * pick.positionScale;
            }
        }

        return positionsOut.Count > 0;
    }
}
