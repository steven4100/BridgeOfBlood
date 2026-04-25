using BridgeOfBlood.Data.Enemies;
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
        [ReadOnly] public NativeArray<EnemyVitality> Vitality;
        [ReadOnly] public NativeArray<int> EntityIds;
        public NativeList<int> OutIndices;
        public NativeList<int> OutEntityIds;

        public void Execute()
        {
            for (int i = 0; i < Vitality.Length; i++)
            {
                if (Vitality[i].health > 0f)
                    continue;
                OutIndices.Add(i);
                OutEntityIds.Add(EntityIds[i]);
            }
        }
    }

    public void CollectDeadEnemies(EnemyBuffers enemies, NativeList<int> outIndices, NativeList<int> outEntityIds)
    {
        outIndices.Clear();
        outEntityIds.Clear();

        JobHandle h = new CollectDeadEnemiesJob
        {
            Vitality = enemies.Vitality,
            EntityIds = enemies.EntityIds,
            OutIndices = outIndices,
            OutEntityIds = outEntityIds
        }.Schedule();

        h.Complete();
    }
}
