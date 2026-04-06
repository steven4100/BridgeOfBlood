using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using UnityEngine;

[Serializable]
public class ChainBehavior : AttackEntityBehavior
{
    [Tooltip("When false, chain redirect and chain-limit removal are skipped for this entity.")]
    public bool isActive = true;

    public ChainMode mode;
    public int chainCount;
    public float chainRange;
    public ChainTargetSelect targetSelect;
    public bool excludePreviouslyHit;

    public ChainPolicyRuntime ToRuntime()
    {
        return new ChainPolicyRuntime
        {
            isActive = isActive,
            chainCount = chainCount,
            chainRange = chainRange,
            targetSelect = targetSelect,
            excludePreviouslyHit = excludePreviouslyHit,
            enabled = mode == ChainMode.Enabled && chainCount > 0 && chainRange > 0f,
            chainHitsSoFar = 0
        };
    }

    public override AttackEntityBehavior Clone() => new ChainBehavior
    {
        isActive = isActive, mode = mode, chainCount = chainCount,
        chainRange = chainRange, targetSelect = targetSelect, excludePreviouslyHit = excludePreviouslyHit
    };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.chain = ToRuntime();

    public override void ApplyModifications(SpellModifications mods, SpellAttributeMask spellMask)
    {
        var resolved = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.Chains, spellMask);
        chainCount = Mathf.Max(0, (int)(chainCount * resolved.Multiplier) + (int)resolved.flat);
    }
}

/// <summary>
/// Max entity IDs to remember for ExcludePreviouslyHit across frames. FixedList32Bytes (32 bytes, ~2 byte overhead) fits 7 ints.
/// </summary>
public static class ChainPolicyConstants
{
    public const int MaxPreviouslyHitIds = 7;
}

/// <summary>
/// Runtime chain policy and state per attack entity. Holds config and runtime data for multi-frame chaining:
/// projectile hits a target (frame N), is redirected to the next target, hits again (frame N+k), up to chainCount.
/// </summary>
public struct ChainPolicyRuntime
{
    /// <summary>When false, chain redirect and chain-limit removal are skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Max number of chain redirects (config).</summary>
    public int chainCount;
    /// <summary>Max distance for the next chain target (config).</summary>
    public float chainRange;
    public ChainTargetSelect targetSelect;
    public bool excludePreviouslyHit;
    public bool enabled;

    /// <summary>Number of chain hits so far (0 = only initial hit). Stops redirecting when chainHitsSoFar >= chainCount.</summary>
    public int chainHitsSoFar;
    /// <summary>Entity IDs of enemies already hit by this attack (for ExcludePreviouslyHit across frames).</summary>
    public FixedList32Bytes<int> hitEnemyIds;

    public static ChainPolicyRuntime Default() => new ChainPolicyRuntime { isActive = false, enabled = false, chainCount = 0, chainRange = 0f };
}

/// <summary>
/// How to select the next chain target. More modes can be added later.
/// </summary>
public enum ChainTargetSelect
{
    Nearest
}

/// <summary>
/// Whether chaining is active for this attack entity.
/// </summary>
public enum ChainMode
{
    Disabled,
    Enabled
}
