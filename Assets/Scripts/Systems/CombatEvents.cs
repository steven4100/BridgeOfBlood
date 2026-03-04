using Unity.Mathematics;

/// <summary>
/// One resolved hit: which attack hit which enemy and where. No damage values; downstream looks up the attack entity.
/// Blittable for NativeList.
/// </summary>
public struct HitEvent
{
    public int attackEntityIndex;
    public int enemyIndex;
    public float2 hitPosition;
}

/// <summary>
/// Reason a projectile is being removed. Used for telemetry, VFX, or debugging.
/// </summary>
public enum AttackEntityRemovalReason
{
    PierceLimitReached,
    ExpiredByTime,
    ExpiredByDistance,
    ChainLimitReached
}

/// <summary>
/// Request to remove an attack entity at end of simulation. Projectile systems (pierce, expiration, etc.)
/// append to a shared list; the list is resolved once at the end of the frame.
/// </summary>
public struct AttackEntityRemovalEvent
{
    public int entityId;
    public AttackEntityRemovalReason reason;
}

/// <summary>
/// Emitted by DamageSystem for each hit that deals damage. Consumed by the damage number / text system.
/// Blittable for NativeList.
/// </summary>
public struct DamageEvent
{
    public float2 position;
    public float damageDealt;
    /// <summary>Index into the enemies array at emit time (so consumers can look up target for presentation, e.g. velocity).</summary>
    public int enemyIndex;
    public bool isCrit;
}
