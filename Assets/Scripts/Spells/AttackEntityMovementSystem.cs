using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct MoveAttackEntitiesJob : IJobParallelFor
{
    public NativeArray<AttackEntity> Entities;
    [ReadOnly] public NativeArray<byte> Alive;
    public NativeArray<MotionPolicyRuntime> MotionPolicies;
    public float DeltaTime;

    public void Execute(int index)
    {
        if (Alive[index] == 0)
            return;

        AttackEntity e = Entities[index];

        MotionPolicyRuntime policy = MotionPolicies[index];
        if (policy.isActive)
            ApplyTangentialWaves(ref e, in policy);

        float2 displacement = e.velocity * DeltaTime;
        e.position += displacement;
        e.distanceTravelled += math.length(displacement);

        Entities[index] = e;
    }

    /// <summary>
    /// Sums the wave stack into a scalar tangential force, applies it perpendicular to velocity,
    /// then renormalizes to the policy speed so waves steer direction only.
    /// </summary>
    private void ApplyTangentialWaves(ref AttackEntity e, in MotionPolicyRuntime policy)
    {
        float speedSq = math.lengthsq(e.velocity);
        if (speedSq < 1e-8f)
            return;

        float sampleTime = e.timeAlive;
        float force = 0f;
        for (int w = 0; w < policy.waves.Length; w++)
        {
            MotionWaveRuntime wave = policy.waves[w];
            force += MotionWaveSampler.Sample((MotionWaveType)wave.type, sampleTime, wave.frequency, wave.phase, wave.amplitude, wave.pulseWidth, wave.offset);
        }

        float2 tangent = math.normalize(new float2(-e.velocity.y, e.velocity.x));
        float2 steered = e.velocity + tangent * (force * DeltaTime);

        float steeredLen = math.length(steered);
        if (steeredLen < 1e-6f)
            return;

        e.velocity = steered * (policy.speed / steeredLen);
    }
}

public class AttackEntityMovementSystem
{
    public void MoveEntities(
        NativeArray<AttackEntity> entities,
        NativeArray<byte> alive,
        NativeArray<MotionPolicyRuntime> motionPolicies,
        float deltaTime)
    {
        if (entities.Length == 0) return;

        var job = new MoveAttackEntitiesJob
        {
            Entities = entities,
            Alive = alive,
            MotionPolicies = motionPolicies,
            DeltaTime = deltaTime
        };

        int batchSize = math.max(1, entities.Length / 32);
        job.Schedule(entities.Length, batchSize).Complete();
    }
}
