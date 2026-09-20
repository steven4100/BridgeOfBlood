using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class ExpirationBehavior : ModifiableAttackEntityBehavior
{
    [Tooltip("When false, expiration logic is skipped (no time/distance removal).")]
    public bool isActive = true;

    [Tooltip("Lifetime in seconds. 0 = no time limit.")]
    public float maxTimeAlive;

    [Tooltip("Max distance travelled. 0 = no distance limit.")]
    public float maxDistanceTravelled;

    [Tooltip("Max attack time ticks (simulation frames that run the attack time step). 0 = no frame limit.")]
    public int maxFrames;

    public ExpirationPolicyRuntime ToRuntime()
    {
        return new ExpirationPolicyRuntime
        {
            isActive = isActive,
            maxTimeAlive = maxTimeAlive,
            maxDistanceTravelled = maxDistanceTravelled,
            maxFrames = maxFrames
        };
    }

    public override AttackEntityBehavior Clone() => new ExpirationBehavior { isActive = isActive, maxTimeAlive = maxTimeAlive, maxDistanceTravelled = maxDistanceTravelled, maxFrames = maxFrames };

    public override void ApplyTo(AttackEntityManager manager, int index, ModificationSet mods, SpellAttributeMask mask, ref Unity.Mathematics.Random rng)
    {
        var arr = manager.GetExpirationPolicies();
        var policy = ToRuntime();
        policy.maxTimeAlive = ResolveMaxTimeAlive(mods, mask);
        arr[index] = policy;
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        if (!isActive)
            return string.Empty;
        float duration = ResolveMaxTimeAlive(mods, mask);
        if (maxTimeAlive > 0f && maxDistanceTravelled > 0f)
            return $"Duration : {duration:0.##}s Range : {maxDistanceTravelled:0.##}";
        if (maxTimeAlive > 0f)
            return $"Duration : {duration:0.##}s";
        if (maxDistanceTravelled > 0f)
            return $"Range : {maxDistanceTravelled:0.##}";
        return string.Empty;
    }

    float ResolveMaxTimeAlive(ModificationSet mods, SpellAttributeMask mask)
    {
        if (maxTimeAlive <= 0f || mods == null)
            return maxTimeAlive;

        ResolvedModifier duration = ParameterModificationMerger.Merge(
            mods.Get<DurationModificationData>(),
            modification => modification.parameters,
            modification => modification.filter,
            mask);
        return Mathf.Max(0f, (maxTimeAlive + duration.flat) * duration.Multiplier);
    }
}

/// <summary>
/// Runtime expiration policy. State (timeAlive, distanceTravelled) lives on AttackEntity.
/// </summary>
[Serializable]
public struct ExpirationPolicyRuntime
{
    /// <summary>When false, expiration removal is skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Lifetime in seconds. 0 = no limit.</summary>
    public float maxTimeAlive;
    /// <summary>Max distance travelled. 0 = no limit.</summary>
    public float maxDistanceTravelled;
    /// <summary>Max attack time ticks (see <see cref="AttackEntity.framesAlive"/>). 0 = no limit.</summary>
    public int maxFrames;

    public static ExpirationPolicyRuntime Default() => new ExpirationPolicyRuntime { isActive = false, maxTimeAlive = 0f, maxDistanceTravelled = 0f, maxFrames = 0 };
}
