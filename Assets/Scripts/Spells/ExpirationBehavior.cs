using System;
using UnityEngine;

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

    public override AttackEntityBehavior Clone() => new ExpirationBehavior { isActive = isActive, maxTimeAlive = maxTimeAlive, maxDistanceTravelled = maxDistanceTravelled };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.expiration = ToRuntime();
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

    public static ExpirationPolicyRuntime Default() => new ExpirationPolicyRuntime { isActive = false, maxTimeAlive = 0f, maxDistanceTravelled = 0f };
}
