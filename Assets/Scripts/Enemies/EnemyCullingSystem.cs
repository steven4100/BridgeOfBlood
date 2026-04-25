using BridgeOfBlood.Data.Enemies;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct EnemyPastEdgeCullJob : IJob
{
    [ReadOnly] public NativeArray<EnemyMotion> Motion;
    [ReadOnly] public NativeArray<int> EntityIds;
    public float RightEdgeX;
    public NativeList<int> OutIndices;
    public NativeList<int> OutEntityIds;

    public void Execute()
    {
        for (int i = 0; i < Motion.Length; i++)
        {
            if (Motion[i].position.x > RightEdgeX)
            {
                OutIndices.Add(i);
                OutEntityIds.Add(EntityIds[i]);
            }
        }
    }
}

/// <summary>
/// Collects enemies that have left the simulation bounds (e.g. past the right edge).
/// </summary>
public class EnemyCullingSystem
{
    public JobHandle ScheduleCollectEnemiesPastRightEdge(
        EnemyBuffers enemies,
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
            Motion = enemies.Motion,
            EntityIds = enemies.EntityIds,
            RightEdgeX = rightEdgeX,
            OutIndices = outIndices,
            OutEntityIds = outEntityIds
        }.Schedule(dependsOn);
    }
}
