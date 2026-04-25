using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct MoveEnemiesJob : IJobParallelFor
{
    public NativeArray<EnemyMotion> Motion;
    [ReadOnly] public NativeArray<StatusAilmentFlag> Status;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyMotion m = Motion[index];
        if ((Status[index] & (StatusAilmentFlag.Frozen | StatusAilmentFlag.Stunned)) == 0)
        {
            float dx = m.moveSpeed * DeltaTime;
            m.position.x += dx;
        }
        Motion[index] = m;
    }
}

public class EnemyMovementSystemLinear : IEnemyMoveSystem
{
    public void MoveEnemies(EnemyBuffers enemies, float deltaTime)
    {
        if (enemies.Length == 0) return;

        var job = new MoveEnemiesJob
        {
            Motion = enemies.Motion,
            Status = enemies.Status,
            DeltaTime = deltaTime
        };

        int batchCount = math.max(1, enemies.Length / 32);
        var handle = job.Schedule(enemies.Length, batchCount);
        handle.Complete();
    }
}
