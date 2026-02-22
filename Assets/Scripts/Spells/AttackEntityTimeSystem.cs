using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Updates all time-dependent fields on attack entities: timeAlive and hitbox scale growth.
/// Runs as a Burst-compiled parallel job, separate from spatial movement.
/// </summary>
[BurstCompile]
public struct TickAttackEntityTimeJob : IJobParallelFor
{
    public NativeArray<AttackEntity> Entities;
    public float DeltaTime;

    public void Execute(int index)
    {
        AttackEntity e = Entities[index];

        e.timeAlive += DeltaTime;

        if (e.hitBox.scaleGrowthRate > 0f)
            e.currentHitBoxScale += e.hitBox.scaleGrowthRate * DeltaTime;

        Entities[index] = e;
    }
}

public class AttackEntityTimeSystem
{
    public void Tick(NativeArray<AttackEntity> entities, float deltaTime)
    {
        if (entities.Length == 0) return;

        var job = new TickAttackEntityTimeJob
        {
            Entities = entities,
            DeltaTime = deltaTime
        };

        int batchSize = UnityEngine.Mathf.Max(1, entities.Length / 32);
        job.Schedule(entities.Length, batchSize).Complete();
    }
}
