using System;
using Unity.Collections;

/// <summary>
/// One entry in the per-entity rehit list: enemy ID and the attack entity's timeAlive when that enemy was hit.
/// </summary>
[Serializable]
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

    public static RehitPolicyRuntime Default() => new RehitPolicyRuntime { rehitCooldownSeconds = 0f };
}
