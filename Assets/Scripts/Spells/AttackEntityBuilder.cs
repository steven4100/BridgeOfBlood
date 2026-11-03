using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// The single input required to spawn one attack entity. Produced by <see cref="SpellEmissionHandler"/>
/// (per emit) or by combat reactions, and consumed by <see cref="AttackEntityManager.Spawn"/>.
/// <para>
/// Holds authoring data + spell provenance + the modifications/mask to apply at spawn time, plus the
/// transform (position/velocity/direction). There is no intermediate rolled-stats struct: the manager rolls and
/// applies mods directly into its parallel lists when it spawns.
/// </para>
/// </summary>
public readonly struct AttackEntityBuildContext
{
    public readonly AttackEntityData data;
    public readonly int spellId;
    public readonly int spellInvocationId;
    public readonly int keyframeIndex;

    /// <summary>Frame modifications applied at spawn. May be null (no mods).</summary>
    public readonly ModificationSet modifications;

    /// <summary>Attribute mask of the originating spell, used to filter modifiers.</summary>
    public readonly SpellAttributeMask attributeMask;

    public readonly float2 position;
    public readonly float2 velocity;

    /// <summary>Normalized facing. Local +X of a rect hitbox lies along this vector.</summary>
    public readonly float2 direction;

    /// <summary>&lt;= 0 = unused; &gt; 0 = scale total rolled damage to this value (combat reactions ScaleByTriggeringHitDamage).</summary>
    public readonly float eventScaledDamage;

    /// <summary>
    /// Spell that owns this spawn. Hit filtering is read from <see cref="RuntimeSpell.Definition"/>
    /// and <see cref="RuntimeSpell.ExcludedTarget"/> at spawn. Null hits every category.
    /// </summary>
    public readonly RuntimeSpell runtimeSpell;

    public AttackEntityBuildContext(
        AttackEntityData data,
        int spellId,
        int spellInvocationId,
        int keyframeIndex,
        ModificationSet modifications,
        SpellAttributeMask attributeMask,
        float2 position,
        float2 velocity,
        float eventScaledDamage = 0f,
        float2 direction = default,
        RuntimeSpell runtimeSpell = null)
    {
        this.data = data;
        this.spellId = spellId;
        this.spellInvocationId = spellInvocationId;
        this.keyframeIndex = keyframeIndex;
        this.modifications = modifications;
        this.attributeMask = attributeMask;
        this.position = position;
        this.velocity = velocity;
        this.direction = ResolveFacing(direction, velocity);
        this.eventScaledDamage = eventScaledDamage;
        this.runtimeSpell = runtimeSpell;
    }

    /// <summary>
    /// Facing for a spawn. An explicit direction wins; otherwise facing follows velocity.
    /// A zero velocity with no explicit direction faces right.
    /// </summary>
    public static float2 ResolveFacing(float2 direction, float2 velocity)
    {
        if (math.lengthsq(direction) > 1e-12f)
            return math.normalize(direction);
        if (math.lengthsq(velocity) > 1e-12f)
            return math.normalize(velocity);
        return new float2(1f, 0f);
    }

    /// <summary>Returns a copy with the spawn transform filled in. Facing follows velocity, or +X when velocity is zero.</summary>
    public AttackEntityBuildContext WithTransform(float2 position, float2 velocity)
    {
        float2 facing = math.lengthsq(velocity) > 1e-12f
            ? math.normalize(velocity)
            : new float2(1f, 0f);
        return WithTransform(position, velocity, facing);
    }

    /// <summary>Returns a copy with an explicit facing. A zero direction falls back to velocity, then +X.</summary>
    public AttackEntityBuildContext WithTransform(float2 position, float2 velocity, float2 direction)
    {
        return new AttackEntityBuildContext(
            data, spellId, spellInvocationId, keyframeIndex,
            modifications, attributeMask, position, velocity, eventScaledDamage, direction,
            runtimeSpell);
    }
}
