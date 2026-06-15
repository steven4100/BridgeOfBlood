using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;

public class HitConditionalEvaluationSystem
{
    


    public static ResolvedModifier GetResolvedModifierForAttackEntity(int enemyEntityIndex, EnemyBuffers enemies, AttackEntity attackEntity)
    {
        ResolvedModifier modifier = new ResolvedModifier();


        return modifier;
    }

    public static bool EvaluateEnemyPredicate(in AttackPredicate predicate, int enemyEntityIndex, EnemyBuffers enemies)
    {
        float percentHealth = 0;
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
        if(thresholdType == ThresholdType.Under)
        {
            return new AttackPredicate
            {
                attackPredicateKind = AttackPredicateKind.HealthBelowPercentage,
                param1 = (float)healthPercentage,
            };
        }
        else
        {
            return new AttackPredicate
            {
                attackPredicateKind = AttackPredicateKind.HealthAbovePercentage,
                param1 = (float)healthPercentage,
            };
        }
    }
}

public class AttackPredicateEffect : IEffect
{
    [SerializeReference, SerializeInterface]
    public AttackPredicateDataBaker attackPredicateDataBaker;

    public ResolvedModifier resolvedModifier;
    public bool Apply(EffectContext context)
    {
        context.spellModifications.Add(new AttackEntityModifier
        {
            predicate = attackPredicateDataBaker.Bake(),
            resolvedModifier = resolvedModifier
        });

        return true;
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

public class AttackEntityModifier
{
    public AttackPredicate predicate;
    public ResolvedModifier resolvedModifier;
}