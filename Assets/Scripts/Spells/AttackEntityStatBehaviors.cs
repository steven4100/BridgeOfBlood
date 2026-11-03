using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

[Serializable]
public abstract class DamageBehavior : ModifiableAttackEntityBehavior
{
    public FloatRange damageRange;

    protected abstract string DamageLabel { get; }
    protected abstract FloatRange ResolveModifiedRange(ModificationSet mods, SpellAttributeMask mask);
    protected abstract void WriteDamage(AttackEntityManager manager, int index, float rolled);

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        ModificationSet mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        FloatRange range = ResolveModifiedRange(mods, mask);
        float rolled = Mathf.Max(0f, range.ResolveUniform(ref rng));
        WriteDamage(manager, index, rolled);
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        FloatRange range = ResolveModifiedRange(mods, mask);
        return $"{DamageLabel} : {range}";
    }
}

[Serializable]
public class PhysicalDamageBehavior : DamageBehavior
{
    protected override string DamageLabel => "Physical Damage";

    protected override FloatRange ResolveModifiedRange(ModificationSet mods, SpellAttributeMask mask) =>
        AttackEntityModificationApplicator.ResolveDamageRange<PhysicalDamageScalingModificationData>(
            damageRange, mods, mask,
            modification => modification.parameters,
            modification => modification.filter);

    protected override void WriteDamage(AttackEntityManager manager, int index, float rolled)
    {
        var arr = manager.GetPhysicalDamages();
        arr[index] = new PhysicalDamageRuntime { isActive = true, amount = rolled };
    }

    public override AttackEntityBehavior Clone() =>
        new PhysicalDamageBehavior { damageRange = damageRange };
}

[Serializable]
public class ColdDamageBehavior : DamageBehavior
{
    protected override string DamageLabel => "Cold Damage";

    protected override FloatRange ResolveModifiedRange(ModificationSet mods, SpellAttributeMask mask) =>
        AttackEntityModificationApplicator.ResolveDamageRange<ColdDamageScalingModificationData>(
            damageRange, mods, mask,
            modification => modification.parameters,
            modification => modification.filter);

    protected override void WriteDamage(AttackEntityManager manager, int index, float rolled)
    {
        var arr = manager.GetColdDamages();
        arr[index] = new ColdDamageRuntime { isActive = true, amount = rolled };
    }

    public override AttackEntityBehavior Clone() =>
        new ColdDamageBehavior { damageRange = damageRange };
}

[Serializable]
public class FireDamageBehavior : DamageBehavior
{
    protected override string DamageLabel => "Fire Damage";

    protected override FloatRange ResolveModifiedRange(ModificationSet mods, SpellAttributeMask mask) =>
        AttackEntityModificationApplicator.ResolveDamageRange<FireDamageScalingModificationData>(
            damageRange, mods, mask,
            modification => modification.parameters,
            modification => modification.filter);

    protected override void WriteDamage(AttackEntityManager manager, int index, float rolled)
    {
        var arr = manager.GetFireDamages();
        arr[index] = new FireDamageRuntime { isActive = true, amount = rolled };
    }

    public override AttackEntityBehavior Clone() =>
        new FireDamageBehavior { damageRange = damageRange };
}

[Serializable]
public class LightningDamageBehavior : DamageBehavior
{
    protected override string DamageLabel => "Lightning Damage";

    protected override FloatRange ResolveModifiedRange(ModificationSet mods, SpellAttributeMask mask) =>
        AttackEntityModificationApplicator.ResolveDamageRange<LightningDamageScalingModificationData>(
            damageRange, mods, mask,
            modification => modification.parameters,
            modification => modification.filter);

    protected override void WriteDamage(AttackEntityManager manager, int index, float rolled)
    {
        var arr = manager.GetLightningDamages();
        arr[index] = new LightningDamageRuntime { isActive = true, amount = rolled };
    }

    public override AttackEntityBehavior Clone() =>
        new LightningDamageBehavior { damageRange = damageRange };
}

[Serializable]
public class CritBehavior : ModifiableAttackEntityBehavior
{
    public FloatRange critChanceRange;
    public FloatRange critDamageMultiplierRange;

    public override AttackEntityBehavior Clone() => new CritBehavior
    {
        critChanceRange = critChanceRange,
        critDamageMultiplierRange = critDamageMultiplierRange
    };

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        ModificationSet mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        FloatRange chanceR = critChanceRange;
        FloatRange multR = critDamageMultiplierRange;
        if (mods != null)
        {
            var critChance = ParameterModificationMerger.Merge(
                mods.Get<CritChanceModificationData>(),
                modification => modification.parameters,
                modification => modification.filter,
                mask);
            chanceR = new FloatRange
            {
                min = Mathf.Clamp01(critChanceRange.min * critChance.Multiplier + critChance.flat / 100f),
                max = Mathf.Clamp01(critChanceRange.max * critChance.Multiplier + critChance.flat / 100f)
            };
            chanceR.ClampOrder();

            var critMult = ParameterModificationMerger.Merge(
                mods.Get<CritMultiplierModificationData>(),
                modification => modification.parameters,
                modification => modification.filter,
                mask);
            multR = new FloatRange
            {
                min = Mathf.Max(1f, critDamageMultiplierRange.min * critMult.Multiplier + critMult.flat / 100f),
                max = Mathf.Max(1f, critDamageMultiplierRange.max * critMult.Multiplier + critMult.flat / 100f)
            };
            multR.ClampOrder();
        }

        var arr = manager.GetCrits();
        arr[index] = new CritRuntime
        {
            isActive = true,
            chance = Mathf.Clamp01(chanceR.ResolveUniform(ref rng)),
            multiplier = Mathf.Max(1f, multR.ResolveUniform(ref rng))
        };
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        FloatRange chanceR = critChanceRange;
        FloatRange multR = critDamageMultiplierRange;
        var critChance = mods == null
            ? ResolvedModifier.Identity
            : ParameterModificationMerger.Merge(
                mods.Get<CritChanceModificationData>(),
                modification => modification.parameters,
                modification => modification.filter,
                mask);
        chanceR = new FloatRange
        {
            min = Mathf.Clamp01(critChanceRange.min * critChance.Multiplier + critChance.flat / 100f),
            max = Mathf.Clamp01(critChanceRange.max * critChance.Multiplier + critChance.flat / 100f)
        };
        chanceR.ClampOrder();

        var critMult = mods == null
            ? ResolvedModifier.Identity
            : ParameterModificationMerger.Merge(
                mods.Get<CritMultiplierModificationData>(),
                modification => modification.parameters,
                modification => modification.filter,
                mask);
        multR = new FloatRange
        {
            min = Mathf.Max(1f, critDamageMultiplierRange.min * critMult.Multiplier + critMult.flat / 100f),
            max = Mathf.Max(1f, critDamageMultiplierRange.max * critMult.Multiplier + critMult.flat / 100f)
        };
        multR.ClampOrder();
        return $"Crit Chance : {chanceR} Mult : {multR}";
    }
}

[Serializable]
public class HitBoxBehavior : ModifiableAttackEntityBehavior
{
    public HitBoxData hitBoxData;

    public override AttackEntityBehavior Clone() => new HitBoxBehavior { hitBoxData = hitBoxData };

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        ModificationSet mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        var arr = manager.GetHitBoxes();
        arr[index] = new HitBoxRuntime
        {
            isActive = true,
            hitBox = AttackEntityModificationApplicator.ResolveHitBox(hitBoxData, mods, mask),
            currentScale = 1f,
            canHit = HitTarget.All,
            excludedTarget = EntityId.Invalid
        };
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        ResolvedModifier mod = mods == null
            ? ResolvedModifier.Identity
            : ParameterModificationMerger.Merge(
                mods.Get<AreaOfEffectModificationData>(),
                modification => modification.parameters,
                modification => modification.filter,
                mask);
        return $"Aoe : {mod.TotalIncrease().ToString()}";
    }
}

[Serializable]
public class KnockbackBehavior : ModifiableAttackEntityBehavior
{
    public float knockbackStrength;

    /// <summary>When set, every hit is pushed along the attack entity's facing instead of radially away from its center.</summary>
    public bool useEntityDirection;

    public override AttackEntityBehavior Clone() =>
        new KnockbackBehavior
        {
            knockbackStrength = knockbackStrength,
            useEntityDirection = useEntityDirection
        };

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        ModificationSet mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        var arr = manager.GetKnockbacks();
        arr[index] = new KnockbackRuntime
        {
            isActive = true,
            useEntityDirection = useEntityDirection,
            strength = ResolveKnockback(mods, mask)
        };
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        return $"Knockback : {ResolveKnockback(mods, mask)}";
    }

    float ResolveKnockback(ModificationSet mods, SpellAttributeMask mask)
    {
        float knockback = Mathf.Max(0f, knockbackStrength);
        if (mods == null)
            return knockback;
        var knock = ParameterModificationMerger.Merge(
            mods.Get<KnockbackStrengthModificationData>(),
            modification => modification.parameters,
            modification => modification.filter,
            mask);
        return Mathf.Max(0f, knockbackStrength * knock.Multiplier + knock.flat);
    }
}

[Serializable]
public struct PhysicalDamageRuntime
{
    public bool isActive;
    public float amount;

    public static PhysicalDamageRuntime Default() => new PhysicalDamageRuntime { isActive = false, amount = 0f };
}

[Serializable]
public struct ColdDamageRuntime
{
    public bool isActive;
    public float amount;

    public static ColdDamageRuntime Default() => new ColdDamageRuntime { isActive = false, amount = 0f };
}

[Serializable]
public struct FireDamageRuntime
{
    public bool isActive;
    public float amount;

    public static FireDamageRuntime Default() => new FireDamageRuntime { isActive = false, amount = 0f };
}

[Serializable]
public struct LightningDamageRuntime
{
    public bool isActive;
    public float amount;

    public static LightningDamageRuntime Default() => new LightningDamageRuntime { isActive = false, amount = 0f };
}

[Serializable]
public struct CritRuntime
{
    public bool isActive;
    public float chance;
    public float multiplier;

    public static CritRuntime Default() => new CritRuntime { isActive = false, chance = 0f, multiplier = 1f };
}

/// <summary>Categories an attack hitbox is allowed to overlap.</summary>
[Flags]
public enum HitTarget : byte
{
    Enemies = 1 << 0,
    MapObjects = 1 << 1,
    All = Enemies | MapObjects
}

[Serializable]
public struct HitBoxRuntime
{
    public bool isActive;
    public HitBoxData hitBox;
    public float currentScale;

    /// <summary>Target categories this hitbox can overlap. <see cref="excludedTarget"/> is skipped even when its category is allowed.</summary>
    public HitTarget canHit;

    /// <summary>Specific entity this hitbox cannot hit. Invalid means no exclusion.</summary>
    public EntityId excludedTarget;

    public static HitBoxRuntime Default() => new HitBoxRuntime
    {
        isActive = false,
        currentScale = 1f,
        canHit = HitTarget.All,
        excludedTarget = EntityId.Invalid
    };

    public bool Allows(HitTarget target, EntityId targetId)
    {
        if ((canHit & target) == 0)
            return false;
        return !excludedTarget.IsValid || excludedTarget != targetId;
    }
}

[Serializable]
public struct KnockbackRuntime
{
    public bool isActive;
    public bool useEntityDirection;
    public float strength;

    public static KnockbackRuntime Default() => new KnockbackRuntime { isActive = false, useEntityDirection = false, strength = 0f };
}
