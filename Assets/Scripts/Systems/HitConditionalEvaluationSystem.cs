using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using Unity.Collections;
using UnityEngine;

[Serializable]
public abstract class HitModifier : IEffect
{
    public virtual bool Apply(EffectContext context)
    {
        context.spellModifications.Add(this);
        return true;
    }

    public abstract void Modify(NativeList<HitEvent> hits, EnemyBuffers enemies, int ownerSpellId);

    public abstract string GetDescription();
}

[Serializable, MenuPath("Hit")]
public class ConditionalHitModifier : HitModifier
{
    [SerializeReference, SerializeInterface]
    public AttackPredicateDataBaker attackPredicateDataBaker;

    [Tooltip("Which stat the conditional modifier targets when the predicate matches.")]
    public SpellModificationProperty property;

    public ResolvedModifier resolvedModifier;

    AttackPredicate predicate;

    public override bool Apply(EffectContext context)
    {
        predicate = attackPredicateDataBaker.Bake();
        return base.Apply(context);
    }

    public override string GetDescription()
    {
        string then = EffectDescription.Describe(resolvedModifier, property);
        string when = attackPredicateDataBaker.GetDescription();
        if (string.IsNullOrEmpty(then))
            return $"If {when}";
        return $"If {when}, then {then}";
    }

    public override void Modify(NativeList<HitEvent> hits, EnemyBuffers enemies, int ownerSpellId)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            HitEvent hit = hits[i];
            if (ownerSpellId != 0 && hit.spellId != ownerSpellId)
                continue;
            if (!enemies.IsValid(hit.enemyEntityId))
                continue;
            if (!HitModifierSystem.EvaluateEnemyPredicate(predicate, hit.enemyEntityId.Index, enemies))
                continue;

            float physical = hit.physicalDamage;
            float cold = hit.coldDamage;
            float fire = hit.fireDamage;
            float lightning = hit.lightningDamage;
            float critChance = hit.critChance;
            float critMult = hit.critMultiplier;

            AttackEntityModificationApplicator.Apply(
                property, resolvedModifier,
                ref physical, ref cold, ref fire, ref lightning, ref critChance, ref critMult);

            hit.physicalDamage = physical;
            hit.coldDamage = cold;
            hit.fireDamage = fire;
            hit.lightningDamage = lightning;
            hit.critChance = critChance;
            hit.critMultiplier = critMult;
            hits[i] = hit;
        }
    }
}

public class HitModifierSystem
{
    public void Apply(ModificationCatalog catalog, NativeList<HitEvent> hits, EnemyBuffers enemies)
    {
        if (catalog == null)
            return;

        ApplySet(catalog.Global.hitModifiers, hits, enemies, 0);

        foreach (KeyValuePair<int, ModificationSet> entry in catalog.PerSpell)
            ApplySet(entry.Value.hitModifiers, hits, enemies, entry.Key);
    }

    static void ApplySet(
        List<HitModifier> modifiers,
        NativeList<HitEvent> hits,
        EnemyBuffers enemies,
        int ownerSpellId)
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].Modify(hits, enemies, ownerSpellId);
    }

    public static bool EvaluateEnemyPredicate(in AttackPredicate predicate, int enemyEntityIndex, EnemyBuffers enemies)
    {
        float percentHealth;
        switch (predicate.attackPredicateKind)
        {
            case AttackPredicateKind.HasAilmentFlag:
                return enemies.Status[enemyEntityIndex].HasFlag((StatusAilmentFlag)predicate.param0);
            case AttackPredicateKind.HealthBelowPercentage:
                percentHealth = enemies.Vitality[enemyEntityIndex].health / enemies.Vitality[enemyEntityIndex].maxHealth;
                return percentHealth < predicate.param1;
            case AttackPredicateKind.HealthAbovePercentage:
                percentHealth = enemies.Vitality[enemyEntityIndex].health / enemies.Vitality[enemyEntityIndex].maxHealth;
                return percentHealth > predicate.param1;
            default:
                Debug.LogError("Failed to evaluate predicate");
                return false;
        }
    }
}

public abstract class AttackPredicateDataBaker
{
    public abstract AttackPredicate Bake();
    public abstract string GetDescription();
}

public class HasAilmentAttackPredicateBaker : AttackPredicateDataBaker
{
    public StatusAilmentFlag ailmentFlag;

    public override AttackPredicate Bake()
    {
        return new AttackPredicate
        {
            attackPredicateKind = AttackPredicateKind.HasAilmentFlag,
            param0 = (int)ailmentFlag,
        };
    }

    public override string GetDescription() =>
        $"the enemy is {EffectDescription.AilmentNames(ailmentFlag)}";
}

public class HealthPercentagePredicateBaker : AttackPredicateDataBaker
{
    public enum ThresholdType
    {
        Under,
        Over
    }

    public ThresholdType thresholdType;
    public float healthPercentage;

    public override AttackPredicate Bake()
    {
        if (thresholdType == ThresholdType.Under)
        {
            return new AttackPredicate
            {
                attackPredicateKind = AttackPredicateKind.HealthBelowPercentage,
                param1 = healthPercentage,
            };
        }

        return new AttackPredicate
        {
            attackPredicateKind = AttackPredicateKind.HealthAbovePercentage,
            param1 = healthPercentage,
        };
    }

    public override string GetDescription()
    {
        string pct = EffectDescription.FormatNumber(healthPercentage * 100f);
        return thresholdType == ThresholdType.Under
            ? $"the enemy is below {pct}% health"
            : $"the enemy is above {pct}% health";
    }
}

public struct AttackPredicate
{
    public AttackPredicateKind attackPredicateKind;
    public int param0;
    public float param1;
}

public enum AttackPredicateKind
{
    HasAilmentFlag,
    HealthBelowPercentage,
    HealthAbovePercentage,
}
