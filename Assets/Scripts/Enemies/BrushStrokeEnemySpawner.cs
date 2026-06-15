using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Input-driven spawner: brush fill defines final positions; owned table picks enemy type only (no <see cref="SpawnPattern"/>).
/// </summary>
[Serializable]
public class BrushStrokeEnemySpawner : IEnemySpawner
{
    public EnemySpawnTable spawnTable;

    [Tooltip("Brush radius in simulation-zone local units.")]
    [Min(0.5f)]
    public float brushRadius = 12f;

    [Tooltip("Spawn points per unit area inside the brush circle.")]
    [Min(0.001f)]
    public float brushDensity = 0.08f;

    [Tooltip("Minimum distance between stroke samples along the drag path.")]
    [Min(0.25f)]
    public float sampleSpacing = 6f;

    readonly List<Vector2> _pendingPositions = new List<Vector2>();
    Vector2? _lastStrokeSample;

    public float BrushRadius
    {
        get => brushRadius;
        set => brushRadius = Mathf.Max(0.5f, value);
    }

    public void Reset()
    {
        _pendingPositions.Clear();
        _lastStrokeSample = null;
    }

    public List<EnemySpawnRequest> CollectSpawnRequests(float simulationTime, Rect playfield)
    {
        if (_pendingPositions.Count == 0 || spawnTable == null)
            return new List<EnemySpawnRequest>();

        var positions = new List<Vector2>(_pendingPositions);
        _pendingPositions.Clear();

        uint seed = (uint)(simulationTime * 1000f).GetHashCode() ^ (uint)positions.Count;
        EnemySpawnPick pick = spawnTable.PickEnemyByWeight(seed);
        if (pick.enemy == null)
            return new List<EnemySpawnRequest>();

        return new List<EnemySpawnRequest>(1)
        {
            new EnemySpawnRequest { enemy = pick.enemy, positions = positions }
        };
    }

    /// <summary>Starts a new stroke; call before drag samples.</summary>
    public void BeginStroke()
    {
        _lastStrokeSample = null;
    }

    /// <summary>Ends the current stroke.</summary>
    public void EndStroke()
    {
        _lastStrokeSample = null;
    }

    /// <summary>
    /// Adds enemies for one brush stamp at playfield-local position. Skips if too close to the previous sample on the same stroke.
    /// </summary>
    public void TryAddStrokeSample(Vector2 playfieldLocal)
    {
        if (_lastStrokeSample.HasValue)
        {
            float spacing = Mathf.Max(0.25f, sampleSpacing);
            if ((playfieldLocal - _lastStrokeSample.Value).sqrMagnitude < spacing * spacing)
                return;
        }

        _lastStrokeSample = playfieldLocal;
        StampBrush(playfieldLocal);
    }

    void StampBrush(Vector2 centerLocal)
    {
        float area = Mathf.PI * brushRadius * brushRadius;
        int count = Mathf.Max(1, Mathf.RoundToInt(area * brushDensity));

        var shape = new SpawnShape
        {
            type = SpawnShapeType.Circle,
            center = centerLocal,
            size = new Vector2(brushRadius, 0f)
        };

        uint seed = math.hash(new float3(centerLocal.x, centerLocal.y, _pendingPositions.Count));
        var rng = Unity.Mathematics.Random.CreateFromIndex(seed);

        for (int i = 0; i < count; i++)
            _pendingPositions.Add(shape.GetRandomPoint(rng.NextFloat(), rng.NextFloat()));
    }
}
