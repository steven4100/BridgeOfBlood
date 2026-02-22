using BridgeOfBlood.Data.Enemies;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct MoveEnemiesJob : IJobParallelFor
{
    public NativeArray<Enemy> Enemies;
    public float DeltaTime;

    public void Execute(int index)
    {
        Enemy e = Enemies[index];
        e.position.x += e.moveSpeed * DeltaTime;
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
