using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Service that provides enemy positions for emitters using <see cref="EmitTargetMode.NearestEnemies"/>.
/// </summary>
public interface IEmissionTargetProvider
{
    /// <summary>
    /// Fills <paramref name="outPositions"/> with up to <paramref name="maxResults"/> nearest enemy positions
    /// within <paramref name="range"/> of <paramref name="position"/>. Map objects are skipped.
    /// <paramref name="excludedTarget"/> is skipped when valid. Clears the list first.
    /// </summary>
    void FindNearby(float2 position, float range, int maxResults, List<float2> outPositions, EntityId excludedTarget);
}

/// <summary>
/// Context passed to <see cref="AttackEntityEmitter.GetEmitPoints"/> each time an emission fires.
/// Bundles the emission origin, facing, count, and an optional enemy-targeting service so emitters
/// can evaluate targets at fire time rather than being pre-computed upfront.
/// </summary>
public struct SpellEmissionContext
{
    public float2 origin;
    public float2 forward;
    public int count;
    public IEmissionTargetProvider targetProvider;
    public EntityId excludedTarget;
}

/// <summary>
/// <see cref="IEmissionTargetProvider"/> backed by <see cref="EnemyManager"/> and its spatial grid.
/// Uses <see cref="GridSpatialPartition.QueryNeighbors"/> for broad-phase, then filters by actual distance.
/// </summary>
public class EnemyEmissionTargetProvider : IEmissionTargetProvider, System.IDisposable
{
    private readonly EnemyManager _enemyManager;
    private NativeList<int> _queryBuffer;
    private readonly List<float> _distanceSq = new List<float>();

    public EnemyEmissionTargetProvider(EnemyManager enemyManager)
    {
        _enemyManager = enemyManager;
        _queryBuffer = new NativeList<int>(64, Allocator.Persistent);
    }

    public void FindNearby(float2 position, float range, int maxResults, List<float2> outPositions, EntityId excludedTarget)
    {
        outPositions.Clear();
        _distanceSq.Clear();
        if (_enemyManager.EnemyCount == 0 || range <= 0f || maxResults <= 0)
            return;

        _queryBuffer.Clear();
        _enemyManager.Grid.QueryNeighbors(position, range, _queryBuffer);
        EnemyBuffers buf = _enemyManager.GetBuffers();
        float rangeSq = range * range;

        for (int i = 0; i < _queryBuffer.Length; i++)
        {
            int idx = _queryBuffer[i];
            if (!buf.IsLive(idx) || buf.Ids[idx].IsMapObject != 0)
                continue;
            if (excludedTarget.IsValid && buf.GetEntityId(idx) == excludedTarget)
                continue;

            float2 enemyPosition = buf.Motion[idx].position;
            float distSq = math.distancesq(position, enemyPosition);
            if (distSq > rangeSq)
                continue;

            InsertNearest(outPositions, _distanceSq, enemyPosition, distSq, maxResults);
        }
    }

    static void InsertNearest(
        List<float2> positions,
        List<float> distances,
        float2 point,
        float distSq,
        int maxResults)
    {
        int insertAt = positions.Count;
        for (int i = 0; i < positions.Count; i++)
        {
            if (distSq < distances[i])
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt >= maxResults)
            return;

        positions.Insert(insertAt, point);
        distances.Insert(insertAt, distSq);
        if (positions.Count > maxResults)
        {
            positions.RemoveAt(positions.Count - 1);
            distances.RemoveAt(distances.Count - 1);
        }
    }

    public void Dispose()
    {
        if (_queryBuffer.IsCreated) _queryBuffer.Dispose();
    }
}
