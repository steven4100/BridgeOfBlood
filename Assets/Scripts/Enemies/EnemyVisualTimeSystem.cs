using BridgeOfBlood.Data.Enemies;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct TickEnemyVisualTimeJob : IJobParallelFor
{
    public NativeArray<Enemy> Enemies;
    public float DeltaTime;

    public void Execute(int index)
    {
        Enemy e = Enemies[index];
        e.visualTime += DeltaTime;
        Enemies[index] = e;
    }
}

/// <summary>
/// Advances <see cref="Enemy.visualTime"/> for flipbook animation; independent of movement.
/// </summary>
public static class EnemyVisualTimeSystem
{
    public static void Tick(NativeArray<Enemy> enemies, float deltaTime)
    {
        if (!enemies.IsCreated || enemies.Length == 0) return;

        var job = new TickEnemyVisualTimeJob
        {
            Enemies = enemies,
            DeltaTime = deltaTime
        };
        int batchCount = math.max(1, enemies.Length / 32);
        job.Schedule(enemies.Length, batchCount).Complete();
    }
}
