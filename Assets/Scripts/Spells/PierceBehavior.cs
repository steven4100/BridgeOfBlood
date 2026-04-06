using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class PierceBehavior : AttackEntityBehavior
{
    [Tooltip("When false, pierce logic is skipped (unlimited hits, no pierce removal).")]
    public bool isActive = true;

    [Tooltip("Max enemies this attack can hit. 0 = unlimited (pierce forever).")]
    public int maxEnemiesHit;

    public PiercePolicyRuntime ToRuntime()
    {
        return new PiercePolicyRuntime { isActive = isActive, maxEnemiesHit = maxEnemiesHit };
    }

    public override AttackEntityBehavior Clone() => new PierceBehavior { isActive = isActive, maxEnemiesHit = maxEnemiesHit };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.pierce = ToRuntime();

    public override void ApplyModifications(SpellModifications mods, SpellAttributeMask spellMask)
    {
        var resolved = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.Pierce, spellMask);
        maxEnemiesHit = Mathf.Max(0, (int)(maxEnemiesHit * resolved.Multiplier) + (int)resolved.flat);
    }
}

/// <summary>
/// Runtime pierce policy. State (hits so far) lives on AttackEntity.enemiesHit.
/// </summary>
public struct PiercePolicyRuntime
{
    /// <summary>When false, pierce filtering and removal are skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Max enemies this attack can hit. 0 = unlimited.</summary>
    public int maxEnemiesHit;

    public static PiercePolicyRuntime Default() => new PiercePolicyRuntime { isActive = false, maxEnemiesHit = 0 };
}
