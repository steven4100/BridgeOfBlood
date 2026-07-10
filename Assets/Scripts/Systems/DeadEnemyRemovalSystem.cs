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
        [ReadOnly] public NativeArray<EnemyVitality> Vitality;
        [ReadOnly] public NativeArray<uint> Generations;
        [ReadOnly] public NativeArray<byte> Alive;
        public NativeList<EntityId> OutEntityIds;

        public void Execute()
        {
            for (int i = 0; i < Vitality.Length; i++)
            {
                if (Alive[i] == 0)
                    continue;

                if (Vitality[i].health > 0f)
                    continue;
                OutEntityIds.Add(new EntityId { Index = i, Generation = Generations[i] });
            }
        }
    }

    public void CollectDeadEnemies(EnemyBuffers enemies, NativeList<EntityId> outEntityIds)
    {
        outEntityIds.Clear();

        JobHandle h = new CollectDeadEnemiesJob
        {
            Vitality = enemies.Vitality,
            Generations = enemies.Generations,
            Alive = enemies.Alive,
            OutEntityIds = outEntityIds
        }.Schedule();

        h.Complete();
    }
}
