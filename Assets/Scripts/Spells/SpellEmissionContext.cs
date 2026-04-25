using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Service that provides enemy positions for emitters using <see cref="EmitTargetMode.NearestEnemies"/>.
/// </summary>
public interface IEmissionTargetProvider
{
    /// <summary>
    /// Fills <paramref name="outPositions"/> with up to <paramref name="maxResults"/> enemy positions
    /// within <paramref name="range"/> of <paramref name="position"/>. Clears the list first.
    /// </summary>
    void FindNearby(float2 position, float range, int maxResults, List<float2> outPositions);
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
}

/// <summary>
/// <see cref="IEmissionTargetProvider"/> backed by <see cref="EnemyManager"/> and its spatial grid.
/// Uses <see cref="GridSpatialPartition.QueryNeighbors"/> for broad-phase, then filters by actual distance.
/// </summary>
public class EnemyEmissionTargetProvider : IEmissionTargetProvider, System.IDisposable
{
    private readonly EnemyManager _enemyManager;
    private NativeList<int> _queryBuffer;

    public EnemyEmissionTargetProvider(EnemyManager enemyManager)
    {
        _enemyManager = enemyManager;
        _queryBuffer = new NativeList<int>(64, Allocator.Persistent);
    }

    public void FindNearby(float2 position, float range, int maxResults, List<float2> outPositions)
    {
        outPositions.Clear();
        if (_enemyManager.EnemyCount == 0 || range <= 0f || maxResults <= 0)
            return;

        _queryBuffer.Clear();
        _enemyManager.Grid.QueryNeighbors(position, range, _queryBuffer);
        EnemyBuffers buf = _enemyManager.GetBuffers();
        float rangeSq = range * range;

        for (int i = 0; i < _queryBuffer.Length && outPositions.Count < maxResults; i++)
        {
            int idx = _queryBuffer[i];
            if (idx >= buf.Length) continue;
            float2 p = buf.Motion[idx].position;
            if (math.distancesq(position, p) <= rangeSq)
                outPositions.Add(p);
        }
    }

    public void Dispose()
    {
        if (_queryBuffer.IsCreated) _queryBuffer.Dispose();
    }
}
