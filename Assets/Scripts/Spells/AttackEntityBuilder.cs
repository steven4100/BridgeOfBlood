using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// The single input required to spawn one attack entity. Produced by <see cref="SpellEmissionHandler"/>
/// (per keyframe) or by combat reactions, and consumed by <see cref="AttackEntityManager.Spawn"/>.
/// <para>
/// Holds authoring data + spell provenance + the modifications/mask to apply at spawn time, plus the
/// transform (position/velocity). There is no intermediate rolled-stats struct: the manager rolls and
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
    public readonly SpellModifications modifications;

    /// <summary>Attribute mask of the originating spell, used to filter modifiers.</summary>
    public readonly SpellAttributeMask attributeMask;

    public readonly float2 position;
    public readonly float2 velocity;

    /// <summary>&lt;= 0 = unused; &gt; 0 = scale total rolled damage to this value (combat reactions ScaleByTriggeringHitDamage).</summary>
    public readonly float eventScaledDamage;

    public AttackEntityBuildContext(
        AttackEntityData data,
        int spellId,
        int spellInvocationId,
        int keyframeIndex,
        SpellModifications modifications,
        SpellAttributeMask attributeMask,
        float2 position,
        float2 velocity,
        float eventScaledDamage = 0f)
    {
        this.data = data;
        this.spellId = spellId;
        this.spellInvocationId = spellInvocationId;
        this.keyframeIndex = keyframeIndex;
        this.modifications = modifications;
        this.attributeMask = attributeMask;
        this.position = position;
        this.velocity = velocity;
        this.eventScaledDamage = eventScaledDamage;
    }

    /// <summary>Returns a copy with the spawn transform (position/velocity) filled in at flush time.</summary>
    public AttackEntityBuildContext WithTransform(float2 position, float2 velocity)
    {
        return new AttackEntityBuildContext(
            data, spellId, spellInvocationId, keyframeIndex,
            modifications, attributeMask, position, velocity, eventScaledDamage);
    }
}
