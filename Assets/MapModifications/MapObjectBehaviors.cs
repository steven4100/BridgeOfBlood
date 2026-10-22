using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

/// <summary>Every X accepted hits, cast an authored spell Y times from the object.</summary>
[Serializable]
public sealed class EmitAttackEveryHitsBehavior : MapObjectBehavior
{
    [SerializeField, Min(1)] int hitsRequired = 1;
    [SerializeField, Min(1)] int emitCount = 1;
    [SerializeField] SpellAuthoringData spell;

    int _hits;

    public override MapObjectBehavior Clone()
    {
        return new EmitAttackEveryHitsBehavior
        {
            hitsRequired = hitsRequired,
            emitCount = emitCount,
            spell = spell
        };
    }

    public override void OnHit(in MapObjectHit hit, MapObjectCombatContext context)
    {
        if (hitsRequired <= 0 || emitCount <= 0)
            return;

        _hits++;
        if (_hits < hitsRequired)
            return;

        _hits -= hitsRequired;
        context.CastSpell(spell, emitCount);
    }
}

/// <summary>
/// Adds flat damage to later hits from attacks that have struck this object.
/// The bonus is a <see cref="HitModifier"/> matched against the attack id set.
/// </summary>
[Serializable]
public sealed class PassThroughDamageBehavior : MapObjectBehavior
{
    [SerializeField] float bonusDamage;

    readonly HashSet<EntityId> _attackIds = new HashSet<EntityId>();
    readonly PassThroughDamageHitModifier _modifier = new PassThroughDamageHitModifier();

    public override MapObjectBehavior Clone()
    {
        return new PassThroughDamageBehavior { bonusDamage = bonusDamage };
    }

    public override HitModifier HitModifier => _modifier;

    public override void OnHit(in MapObjectHit hit, MapObjectCombatContext context)
    {
        _modifier.bonusDamage = bonusDamage;
        _modifier.attackIds = _attackIds;
        _attackIds.Add(hit.AttackEntityId);
    }
}

/// <summary>Flat damage added to hits whose attack id has passed through a map object.</summary>
public sealed class PassThroughDamageHitModifier : HitModifier
{
    public float bonusDamage;
    public HashSet<EntityId> attackIds;

    public override void Modify(NativeList<HitEvent> hits, EnemyBuffers enemies, int ownerSpellId)
    {
        if (attackIds == null || attackIds.Count == 0 || bonusDamage == 0f)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            HitEvent hit = hits[i];
            if (!attackIds.Contains(hit.attackEntityId))
                continue;

            hit.physicalDamage += bonusDamage;
            hits[i] = hit;
        }
    }

    public override string GetDescription() => $"+{bonusDamage} damage after passing through";
}

/// <summary>Every X observed hit damage, cast an authored spell Y times from the object.</summary>
[Serializable]
public sealed class EmitAttackOnDamageBehavior : MapObjectBehavior
{
    [SerializeField, Min(0.01f)] float damageRequired = 1f;
    [SerializeField, Min(1)] int emitCount = 1;
    [SerializeField] SpellAuthoringData spell;

    float _accumulated;

    public override MapObjectBehavior Clone()
    {
        return new EmitAttackOnDamageBehavior
        {
            damageRequired = damageRequired,
            emitCount = emitCount,
            spell = spell
        };
    }

    public override void OnHit(in MapObjectHit hit, MapObjectCombatContext context)
    {
        if (damageRequired <= 0f || emitCount <= 0)
            return;

        _accumulated += hit.Damage;
        while (_accumulated >= damageRequired)
        {
            _accumulated -= damageRequired;
            context.CastSpell(spell, emitCount);
        }
    }
}
