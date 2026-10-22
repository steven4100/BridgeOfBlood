using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct EnemyPastEdgeCullJob : IJob
{
    [ReadOnly] public NativeArray<EnemyMotion> Motion;
    [ReadOnly] public NativeArray<EnemyId> Ids;
    public float RightEdgeX;
    public NativeList<EntityId> OutEntityIds;

    public void Execute()
    {
        for (int i = 0; i < Motion.Length; i++)
        {
            if (Ids[i].IsAlive == 0 || Ids[i].IsMapObject != 0)
                continue;

            if (Motion[i].position.x > RightEdgeX)
            {
                OutEntityIds.Add(new EntityId { Index = i, Generation = Ids[i].Generation });
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
        NativeList<EntityId> outEntityIds,
        JobHandle dependsOn = default)
    {
        outEntityIds.Clear();
        if (enemies.AliveCount == 0)
            return dependsOn;

        return new EnemyPastEdgeCullJob
        {
            Motion = enemies.Motion,
            Ids = enemies.Ids,
            RightEdgeX = rightEdgeX,
            OutEntityIds = outEntityIds
        }.Schedule(dependsOn);
    }
}
