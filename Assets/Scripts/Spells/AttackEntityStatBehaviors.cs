using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Serializable]
public abstract class DamageBehavior : ModifiableAttackEntityBehavior
{
    public FloatRange damageRange;

    protected abstract SpellModificationProperty TypeScalingProperty { get; }

    protected abstract void WriteDamage(AttackEntityManager manager, int index, float rolled);

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        SpellModifications mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        FloatRange range = AttackEntityModificationApplicator.ResolveDamageRange(
            damageRange, TypeScalingProperty, mods, mask);
        float rolled = Mathf.Max(0f, range.ResolveUniform(ref rng));
        WriteDamage(manager, index, rolled);
    }
}

[Serializable]
public class PhysicalDamageBehavior : DamageBehavior
{
    protected override SpellModificationProperty TypeScalingProperty =>
        SpellModificationProperty.PhysicalDamageScaling;

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
    protected override SpellModificationProperty TypeScalingProperty =>
        SpellModificationProperty.ColdDamageScaling;

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
    protected override SpellModificationProperty TypeScalingProperty =>
        SpellModificationProperty.FireDamageScaling;

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
    protected override SpellModificationProperty TypeScalingProperty =>
        SpellModificationProperty.LightningDamageScaling;

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
        SpellModifications mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        FloatRange chanceR = critChanceRange;
        FloatRange multR = critDamageMultiplierRange;
        if (mods != null)
        {
            var critChance = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.CritChance, mask);
            chanceR = new FloatRange
            {
                min = Mathf.Clamp01(critChanceRange.min * critChance.Multiplier + critChance.flat / 100f),
                max = Mathf.Clamp01(critChanceRange.max * critChance.Multiplier + critChance.flat / 100f)
            };
            chanceR.ClampOrder();

            var critMult = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.CritMult, mask);
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
}

[Serializable]
public class HitBoxBehavior : ModifiableAttackEntityBehavior
{
    public HitBoxData hitBoxData;

    public override AttackEntityBehavior Clone() => new HitBoxBehavior { hitBoxData = hitBoxData };

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        SpellModifications mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        var arr = manager.GetHitBoxes();
        arr[index] = new HitBoxRuntime
        {
            isActive = true,
            hitBox = AttackEntityModificationApplicator.ResolveHitBox(hitBoxData, mods, mask),
            currentScale = 1f
        };
    }
}

[Serializable]
public class KnockbackBehavior : ModifiableAttackEntityBehavior
{
    public float knockbackStrength;

    public override AttackEntityBehavior Clone() =>
        new KnockbackBehavior { knockbackStrength = knockbackStrength };

    public override void ApplyTo(
        AttackEntityManager manager,
        int index,
        SpellModifications mods,
        SpellAttributeMask mask,
        ref Random rng)
    {
        float knockback = Mathf.Max(0f, knockbackStrength);
        if (mods != null)
        {
            var knock = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.KnockbackStrength, mask);
            knockback = Mathf.Max(0f, knockbackStrength * knock.Multiplier + knock.flat);
        }

        var arr = manager.GetKnockbacks();
        arr[index] = new KnockbackRuntime { isActive = true, strength = knockback };
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

[Serializable]
public struct HitBoxRuntime
{
    public bool isActive;
    public HitBoxData hitBox;
    public float currentScale;

    public static HitBoxRuntime Default() => new HitBoxRuntime { isActive = false, currentScale = 1f };
}

[Serializable]
public struct KnockbackRuntime
{
    public bool isActive;
    public float strength;

    public static KnockbackRuntime Default() => new KnockbackRuntime { isActive = false, strength = 0f };
}
