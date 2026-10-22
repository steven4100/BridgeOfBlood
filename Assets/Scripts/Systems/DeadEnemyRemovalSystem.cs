using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Scans enemies in index order and appends removal pairs for any with health at or below zero (indices ascending).
/// </summary>
public class DeadEnemyRemovalSystem
{
    [BurstCompile]
    private struct CollectDeadEnemiesJob : IJob
    {
        [ReadOnly] public NativeArray<EnemyId> Ids;
        [ReadOnly] public NativeArray<EnemyVitality> Vitality;
        public NativeList<EntityId> OutEntityIds;

        public void Execute()
        {
            for (int i = 0; i < Vitality.Length; i++)
            {
                if (Ids[i].IsAlive == 0 || Ids[i].IsMapObject != 0)
                    continue;

                if (Vitality[i].health > 0f)
                    continue;
                OutEntityIds.Add(new EntityId { Index = i, Generation = Ids[i].Generation });
            }
        }
    }

    public void CollectDeadEnemies(EnemyBuffers enemies, NativeList<EntityId> outEntityIds)
    {
        outEntityIds.Clear();

        JobHandle h = new CollectDeadEnemiesJob
        {
            Ids = enemies.Ids,
            Vitality = enemies.Vitality,
            OutEntityIds = outEntityIds
        }.Schedule();

        h.Complete();
    }
}
