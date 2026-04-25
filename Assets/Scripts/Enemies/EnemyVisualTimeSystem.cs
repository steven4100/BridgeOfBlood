using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct TickEnemyVisualTimeJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<StatusAilmentFlag> Status;
    public NativeArray<EnemyPresentation> Presentation;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyPresentation p = Presentation[index];
        if ((Status[index] & StatusAilmentFlag.Frozen) == 0)
            p.visualTime += DeltaTime;
        p.ailmentFlashTimer -= DeltaTime;
        if (p.ailmentFlashTimer < 0f)
            p.ailmentFlashTimer = 0f;
        Presentation[index] = p;
    }
}

/// <summary>
/// Advances flipbook time and decays ailment flash timer.
/// </summary>
public static class EnemyVisualTimeSystem
{
    public static void Tick(EnemyBuffers enemies, float deltaTime)
    {
        if (!enemies.Motion.IsCreated || enemies.Length == 0) return;

        var job = new TickEnemyVisualTimeJob
        {
            Status = enemies.Status,
            Presentation = enemies.Presentation,
            DeltaTime = deltaTime
        };
        int batchCount = math.max(1, enemies.Length / 32);
        job.Schedule(enemies.Length, batchCount).Complete();
    }
}
