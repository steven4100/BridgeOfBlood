using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct MoveAttackEntitiesJob : IJobParallelFor
{
    public NativeArray<AttackEntity> Entities;
    public float DeltaTime;

    public void Execute(int index)
    {
        AttackEntity e = Entities[index];

        float2 displacement = e.velocity * DeltaTime;
        e.position += displacement;
        e.distanceTravelled += math.length(displacement);

        Entities[index] = e;
    }
}

public class AttackEntityMovementSystem
{
    public void MoveEntities(NativeArray<AttackEntity> entities, float deltaTime)
    {
        if (entities.Length == 0) return;

        var job = new MoveAttackEntitiesJob
        {
            Entities = entities,
            DeltaTime = deltaTime
        };

        int batchSize = math.max(1, entities.Length / 32);
        job.Schedule(entities.Length, batchSize).Complete();
    }
}
