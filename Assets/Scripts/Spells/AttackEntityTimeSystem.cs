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
    public NativeArray<HitBoxRuntime> HitBoxes;
    [ReadOnly] public NativeArray<byte> Alive;
    public float DeltaTime;

    public void Execute(int index)
    {
        if (Alive[index] == 0)
            return;

        AttackEntity e = Entities[index];
        e.framesAlive++;
        e.timeAlive += DeltaTime;
        Entities[index] = e;

        HitBoxRuntime hitBox = HitBoxes[index];
        if (hitBox.isActive && hitBox.hitBox.scaleGrowthRate > 0f)
        {
            hitBox.currentScale += hitBox.hitBox.scaleGrowthRate * DeltaTime;
            HitBoxes[index] = hitBox;
        }
    }
}

public class AttackEntityTimeSystem
{
    public void Tick(
        NativeArray<AttackEntity> entities,
        NativeArray<HitBoxRuntime> hitBoxes,
        NativeArray<byte> alive,
        float deltaTime)
    {
        if (entities.Length == 0) return;

        var job = new TickAttackEntityTimeJob
        {
            Entities = entities,
            HitBoxes = hitBoxes,
            Alive = alive,
            DeltaTime = deltaTime
        };

        int batchSize = UnityEngine.Mathf.Max(1, entities.Length / 32);
        job.Schedule(entities.Length, batchSize).Complete();
    }
}
