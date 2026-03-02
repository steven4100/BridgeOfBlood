using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Designer-facing config for emission pattern only: returns spawn position and direction per emit.
/// Does not reference entity data or build payloads; the handler does that with game modifiers.
/// </summary>
[System.Serializable]
public class AttackEntityEmitter
{
    [Tooltip("Total spread in degrees, centered on forward. 0 = all same direction, 30 = narrow cone, 360 = full circle.")]
    [Range(0f, 360f)]
    public float spreadDegrees = 0f;

    [Tooltip("Center of the spread in degrees (0 = right/X+, 90 = up/Y+). Overridden by forward vector at emit time when non-zero.")]
    [Range(-180f, 180f)]
    public float forwardDegrees = 0f;

    [Tooltip("Seconds over which to spread emissions. 0 = all at keyframe time; >0 = first at 0, last at this time, evenly spaced.")]
    [Min(0f)]
    public float emitDuration = 0f;

    [Tooltip("Number of projectiles emitted per keyframe. 1 = single, higher = spread/fan.")]
    [Min(1)]
    public int baseEmitCount = 1;

    [Tooltip("Speed of each emitted projectile (units per second).")]
    [Min(0f)]
    public float speed = 1f;

    [Header("Spawn position")]
    [Tooltip("Offset applied to the given origin (e.g. player position). Final spawn position = origin + offset.")]
    public RelativeToPlayerSpawnCriteria relativeToPlayerSpawnCriteria;

    /// <summary>
    /// Returns one entry per emitted entity: position, direction, and time offset (seconds from keyframe fire). No payload; handler builds that.
    /// </summary>
    /// <param name="origin">World position to spawn from (e.g. player position).</param>
    /// <param name="forward">Normalized facing; if zero, uses forwardDegrees.</param>
    /// <param name="count">Number of emit points (e.g. 1 for single, 21 for +20 projectiles).</param>
    public List<EmitPoint> GetEmitPoints(float2 origin, float2 forward, int count)
    {
        var list = new List<EmitPoint>(count > 0 ? count : 0);
        if (count <= 0)
            return list;

        float2 baseDir = math.lengthsq(forward) > 0.0001f
            ? math.normalize(forward)
            : DegreesToDirection(forwardDegrees);

        var directions = GetDirections(baseDir, count);
        float duration = emitDuration > 0f ? emitDuration : 0f;
        float timeStep = (count > 1 && duration > 0f) ? duration / (count - 1) : 0f;

        float2 offset = new float2(relativeToPlayerSpawnCriteria.offsetFromPlayer.x, relativeToPlayerSpawnCriteria.offsetFromPlayer.y);
        float2 spawnPosition = origin + offset;

        for (int i = 0; i < directions.Count; i++)
        {
            float timeOffset = (count == 1 || duration <= 0f) ? 0f : timeStep * i;
            list.Add(new EmitPoint { position = spawnPosition, direction = directions[i], timeOffset = timeOffset });
        }

        return list;
    }

    /// <summary>
    /// Returns normalized directions: count rays evenly distributed over spreadDegrees centered on forward.
    /// </summary>
    internal List<float2> GetDirections(float2 forward, int count)
    {
        var dirs = new List<float2>(count > 0 ? count : 0);
        if (count <= 0) return dirs;

        float centerDeg = math.degrees(math.atan2(forward.y, forward.x));
        float spread = math.radians(spreadDegrees);

        if (count == 1 || spread <= 0f)
        {
            dirs.Add(forward);
            return dirs;
        }

        float startRad = math.radians(centerDeg - spreadDegrees * 0.5f);
        float stepRad = spread / count;
        for (int i = 0; i < count; i++)
        {
            float rad = startRad + stepRad * i;
            dirs.Add(new float2(math.cos(rad), math.sin(rad)));
        }
        return dirs;
    }

    static float2 DegreesToDirection(float degrees)
    {
        float r = math.radians(degrees);
        return new float2(math.cos(r), math.sin(r));
    }
}

/// <summary>
/// One spawn slot from an emitter: position, normalized direction, and time offset (seconds from keyframe fire). Payload is built by the handler.
/// </summary>
public struct EmitPoint
{
    public float2 position;
    public float2 direction;
    /// <summary>Seconds after the keyframe fire time when this should spawn. 0 = same instant.</summary>
    public float timeOffset;
}
