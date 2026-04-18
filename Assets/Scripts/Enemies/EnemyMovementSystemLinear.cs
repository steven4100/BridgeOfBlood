using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct MoveEnemiesJob : IJobParallelFor
{
    public NativeArray<Enemy> Enemies;
    public float DeltaTime;

    public void Execute(int index)
    {
        Enemy e = Enemies[index];
        if ((e.statusAilmentFlag & (StatusAilmentFlag.Frozen | StatusAilmentFlag.Stunned)) == 0)
        {
            float dx = e.moveSpeed * DeltaTime;
            e.position.x += dx;
        }
        Enemies[index] = e;
    }
}

public class EnemyMovementSystemLinear : IEnemyMoveSystem
{
    public void MoveEnemies(NativeArray<Enemy> enemies, float deltaTime)
    {
        if (enemies.Length == 0) return;

        var job = new MoveEnemiesJob
        {
            Enemies = enemies,
            DeltaTime = deltaTime
        };

        int batchCount = math.max(1, enemies.Length / 32);
        var handle = job.Schedule(enemies.Length, batchCount);
        handle.Complete();
    }
}
