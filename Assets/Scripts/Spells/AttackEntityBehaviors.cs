using System;
using Unity.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// Polymorphic authoring behaviors for [SerializeReference] list in AttackEntityData.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base type for optional attack-entity behaviors. Used only as [SerializeReference] target.
/// </summary>
[Serializable]
public abstract class AttackEntityBehavior
{
}

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
}

[Serializable]
public class ExpirationBehavior : AttackEntityBehavior
{
    [Tooltip("When false, expiration logic is skipped (no time/distance removal).")]
    public bool isActive = true;

    [Tooltip("Lifetime in seconds. 0 = no time limit.")]
    public float maxTimeAlive;

    [Tooltip("Max distance travelled. 0 = no distance limit.")]
    public float maxDistanceTravelled;

    public ExpirationPolicyRuntime ToRuntime()
    {
        return new ExpirationPolicyRuntime
        {
            isActive = isActive,
            maxTimeAlive = maxTimeAlive,
            maxDistanceTravelled = maxDistanceTravelled
        };
    }
}

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

/// <summary>
/// Runtime pierce policy. State (hits so far) lives on AttackEntity.enemiesHit.
/// </summary>
public struct PiercePolicyRuntime
{
    /// <summary>When false, pierce filtering and removal are skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Max enemies this attack can hit. 0 = unlimited.</summary>
    public int maxEnemiesHit;
}

/// <summary>
/// Runtime expiration policy. State (timeAlive, distanceTravelled) lives on AttackEntity.
/// </summary>
public struct ExpirationPolicyRuntime
{
    /// <summary>When false, expiration removal is skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Lifetime in seconds. 0 = no limit.</summary>
    public float maxTimeAlive;
    /// <summary>Max distance travelled. 0 = no limit.</summary>
    public float maxDistanceTravelled;
}

// ─── Rehit cooldown (seconds before same enemy can be hit again) ─────────────────────────────

/// <summary>
/// One entry in the per-entity rehit list: enemy ID and the attack entity's timeAlive when that enemy was hit.
/// </summary>
public struct RehitEntry
{
    public int enemyId;
    public float hitTimeAlive;
}

/// <summary>
/// Runtime rehit policy. Every attack entity has one (default rehitCooldownSeconds = 0 = no cooldown).
/// When rehitCooldownSeconds > 0, we track recent (enemyId, hitTimeAlive) and reject collisions still in cooldown.
/// </summary>
public struct RehitPolicyRuntime
{
    /// <summary>Seconds before the same enemy can be hit again. &lt;= 0 = no cooldown (rehit filtering skipped).</summary>
    public float rehitCooldownSeconds;
    /// <summary>Recent hits (enemyId, timeAlive at hit). When full, evict oldest (smallest hitTimeAlive) before adding.</summary>
    public FixedList128Bytes<RehitEntry> recentHits;
}

