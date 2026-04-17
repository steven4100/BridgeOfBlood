using BridgeOfBlood.Data.Enemies;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct EnemyPastEdgeCullJob : IJob
{
    [ReadOnly] public NativeArray<Enemy> Enemies;
    public float RightEdgeX;
    public NativeList<int> OutIndices;
    public NativeList<int> OutEntityIds;

    public void Execute()
    {
        for (int i = 0; i < Enemies.Length; i++)
        {
            if (Enemies[i].position.x > RightEdgeX)
            {
                OutIndices.Add(i);
                OutEntityIds.Add(Enemies[i].entityId);
            }
        }
    }
}

/// <summary>
/// Collects enemies that have left the simulation bounds (e.g. past the right edge).
/// Appends indices in ascending order (sequential scan).
/// </summary>
public class EnemyCullingSystem
{
    public JobHandle ScheduleCollectEnemiesPastRightEdge(
        NativeArray<Enemy> enemies,
        float rightEdgeX,
        NativeList<int> outIndices,
        NativeList<int> outEntityIds,
        JobHandle dependsOn = default)
    {
        outIndices.Clear();
        outEntityIds.Clear();
        if (enemies.Length == 0)
            return dependsOn;

        return new EnemyPastEdgeCullJob
        {
            Enemies = enemies,
            RightEdgeX = rightEdgeX,
            OutIndices = outIndices,
            OutEntityIds = outEntityIds
        }.Schedule(dependsOn);
    }
}
