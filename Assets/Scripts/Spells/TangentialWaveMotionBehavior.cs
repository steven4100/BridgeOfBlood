using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Wave shape used by <see cref="TangentialWaveMotionBehavior"/>. All shapes output in [-1, 1]
/// over one cycle before amplitude scaling.
/// </summary>
public enum MotionWaveType
{
    Sine,
    Square,
    Sawtooth,
    Triangle
}

/// <summary>
/// One authored wave in the tangential-force stack. Samples are summed additively across the stack.
/// </summary>
[Serializable]
public struct MotionWave
{
    public MotionWaveType type;

    [Tooltip("Peak tangential force of this wave (units/sec^2). Negative flips the bend direction.")]
    public float amplitude;

    [Tooltip("Cycles per second.")]
    public float frequency;

    [Tooltip("Phase offset in cycles (0-1).")]
    [Range(0f, 1f)]
    public float phase;

    [Tooltip("Square wave only: fraction of the cycle spent at +1 (duty cycle). 0.5 = symmetric.")]
    [Range(0.01f, 0.99f)]
    public float pulseWidth;

    [Tooltip("Constant force added to this wave's samples (units/sec^2). Biases the bend toward one side.")]
    public float offset;

    public static MotionWave Default(MotionWaveType type) => new MotionWave
    {
        type = type,
        amplitude = 10f,
        frequency = 1f,
        phase = 0f,
        pulseWidth = 0.5f,
        offset = 0f
    };
}

/// <summary>
/// Shared wave evaluation used by the runtime move job and the inspector preview graph,
/// so both always agree on the shape formulas. Burst-compatible.
/// </summary>
public static class MotionWaveSampler
{
    /// <summary>
    /// Samples one wave at <paramref name="time"/> (seconds). Result is amplitude-scaled, then shifted by <paramref name="offset"/>.
    /// <paramref name="pulseWidth"/> is the square-wave duty cycle (fraction of the cycle at +1); other shapes ignore it.
    /// </summary>
    public static float Sample(MotionWaveType type, float time, float frequency, float phase, float amplitude, float pulseWidth, float offset)
    {
        float u = math.frac(time * frequency + phase);
        float raw;
        switch (type)
        {
            case MotionWaveType.Square:
                // pulseWidth <= 0 means unauthored (legacy data); treat as symmetric.
                float width = pulseWidth <= 0f ? 0.5f : math.clamp(pulseWidth, 0.01f, 0.99f);
                raw = u < width ? 1f : -1f;
                break;
            case MotionWaveType.Sawtooth:
                raw = 2f * u - 1f;
                break;
            case MotionWaveType.Triangle:
                raw = 1f - 4f * math.abs(u - 0.5f);
                break;
            default: // Sine
                raw = math.sin(2f * math.PI * u);
                break;
        }
        return raw * amplitude + offset;
    }
}

/// <summary>
/// Baked wave entry stored per entity in <see cref="MotionPolicyRuntime"/>.
/// </summary>
public struct MotionWaveRuntime
{
    public int type;
    public float amplitude;
    public float frequency;
    public float phase;
    public float pulseWidth;
    public float offset;
}

/// <summary>
/// Runtime tangential-wave motion policy per attack entity. The wave stack is sampled at
/// (timeAlive + phaseSeed), summed into a scalar force, and applied perpendicular to velocity;
/// speed is preserved so waves steer direction only.
/// </summary>
public struct MotionPolicyRuntime
{
    public bool isActive;
    /// <summary>Speed the entity is renormalized to each tick. Set at spawn; resynced on chain redirect.</summary>
    public float speed;
    /// <summary>Per-entity time offset (seconds) so sibling projectiles desync.</summary>
    public float phaseSeed;
    public FixedList512Bytes<MotionWaveRuntime> waves;

    public static MotionPolicyRuntime Default() => new MotionPolicyRuntime
    {
        isActive = false,
        speed = 0f,
        phaseSeed = 0f,
        waves = default
    };
}

/// <summary>
/// Steers attack entities with a composable stack of tangential-force waves
/// (sine / square / sawtooth / triangle). Direction-only: speed is preserved.
/// </summary>
[Serializable]
public class TangentialWaveMotionBehavior : AttackEntityBehavior
{
    /// <summary>Max waves baked into the runtime policy. Extras are ignored.</summary>
    public const int MaxWaves = 8;

    [Tooltip("When false, this entity keeps constant linear velocity.")]
    public bool isActive = true;

    [Tooltip("Waves are sampled at the entity's time alive and summed into one tangential force.")]
    public List<MotionWave> waves = new List<MotionWave>();

    public MotionPolicyRuntime ToRuntime(float2 velocity, int entityId)
    {
        float speed = math.length(velocity);

        var policy = new MotionPolicyRuntime
        {
            isActive = isActive && speed > 0.0001f && waves != null && waves.Count > 0,
            speed = speed,
            phaseSeed = math.hash(new int2(entityId, 0x57415645)) / (float)uint.MaxValue, // "WAVE"
            waves = default
        };

        if (!policy.isActive)
            return policy;

        int count = math.min(waves.Count, MaxWaves);
        for (int i = 0; i < count; i++)
        {
            MotionWave w = waves[i];
            policy.waves.Add(new MotionWaveRuntime
            {
                type = (int)w.type,
                amplitude = w.amplitude,
                frequency = w.frequency,
                phase = w.phase,
                pulseWidth = w.pulseWidth,
                offset = w.offset
            });
        }

        return policy;
    }

    public override AttackEntityBehavior Clone()
    {
        var clone = new TangentialWaveMotionBehavior { isActive = isActive };
        clone.waves = new List<MotionWave>(waves);
        return clone;
    }

    public override void ApplyTo(AttackEntityManager manager, int index, SpellModifications mods, SpellAttributeMask mask)
    {
        AttackEntity entity = manager.GetEntities()[index];
        var arr = manager.GetMotionPolicies();
        arr[index] = ToRuntime(entity.velocity, entity.entityId);
    }
}
