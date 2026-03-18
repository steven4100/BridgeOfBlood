using BridgeOfBlood.Data.Shared;
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
/// Emitted by DamageSystem for each hit that deals damage. Single source of truth for telemetry aggregation
/// and damage number rendering. Per-type damage values include crit scaling and sum to damageDealt.
/// Blittable for NativeList.
/// </summary>
public struct DamageEvent
{
    public float2 position;
    public float damageDealt;
    /// <summary>Index into the enemies array at emit time (so consumers can look up target for presentation, e.g. velocity).</summary>
    public int enemyIndex;
    public bool isCrit;

    public float physicalDamage;
    public float fireDamage;
    public float coldDamage;
    public float lightningDamage;
    public int spellId;
    public int spellInvocationId;
    public bool wasKill;
    public float overkillDamage;
}

/// <summary>
/// Emitted by per-ailment application systems when a status ailment is applied to an enemy.
/// One event per ailment per hit (so a hit that triggers two ailments emits two events).
/// Blittable for NativeList.
/// </summary>
public struct StatusAilmentAppliedEvent
{
    public int spellId;
    public int spellInvocationId;
    public int enemyIndex;
    public StatusAilmentFlag ailmentFlag;
}
