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
    ExpiredByFrames,
    ChainLimitReached,
    CulledOffScreen
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
/// Why an enemy row is being removed from the live enemy buffer. Used to bucket removals in <see cref="EnemyRemovalBatch"/>.
/// </summary>
public enum EnemyRemovalReason
{
    CulledPastBounds,
    HealthDepleted
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
    /// <summary>Index into the attack entities array at emit time (so consumers can look up source for VFX config).</summary>
    public int attackEntityIndex;
    public bool isCrit;

    public float physicalDamage;
    public float fireDamage;
    public float coldDamage;
    public float lightningDamage;
    public int spellId;
    public int spellInvocationId;
    public bool wasKill;
    public float overkillDamage;
    /// <summary>Blood collected from this hit. Currently damageDealt + overkillDamage; future blood multipliers apply here.</summary>
    public float bloodExtracted;
    /// <summary>Snapshotted at hit time so VFX work after the attack entity row is removed (swap-back / expiration).</summary>
    public EffectSpriteConfigRuntime onHitEffectForVfx;
    /// <summary>Snapshotted at hit time; use with <see cref="wasKill"/>.</summary>
    public EffectSpriteConfigRuntime onKillEffectForVfx;
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

/// <summary>Which ailment produced a resolved <see cref="TickDamageEvent"/>.</summary>
public enum TickDamageSource : byte
{
    Fire,
    Poison,
    Bleed
}

/// <summary>
/// Resolved DoT damage for telemetry and numbers. Not a projectile hit; <see cref="AttackEntityIndexNone"/> for attack index.
/// </summary>
public struct TickDamageEvent
{
    public const int AttackEntityIndexNone = -1;

    public float2 position;
    public float damageDealt;
    public int enemyIndex;
    public int spellId;
    public int spellInvocationId;
    public bool wasKill;
    public float overkillDamage;
    public float bloodExtracted;
    public float physicalDamage;
    public float fireDamage;
    public float coldDamage;
    public float lightningDamage;
    public TickDamageSource source;
}
