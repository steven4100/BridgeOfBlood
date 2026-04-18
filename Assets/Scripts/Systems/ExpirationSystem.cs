using Unity.Collections;

/// <summary>
/// Evaluates expiration policy: when an attack entity has exceeded maxFrames, maxTimeAlive, or maxDistanceTravelled,
/// appends an AttackEntityRemovalEvent so the entity is removed at end of frame.
/// Does not remove entities; only appends to the shared removal list.
/// Assumes attackEntities and expirationPolicies have matching length; caller (e.g. AttackEntityManager.ValidateParallelLists) must validate upstream.
/// </summary>
public class ExpirationSystem
{
    /// <summary>
    /// For each attack entity that has exceeded time or distance limits,
    /// appends a removal event. Call after movement and time have been ticked.
    /// </summary>
    public void CollectRemovals(
        NativeArray<AttackEntity> attackEntities,
        NativeArray<ExpirationPolicyRuntime> expirationPolicies,
        NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        for (int i = 0; i < attackEntities.Length; i++)
        {
            AttackEntity e = attackEntities[i];
            ExpirationPolicyRuntime exp = expirationPolicies[i];
            if (!exp.isActive) continue;
            bool expired = false;
            AttackEntityRemovalReason reason = default;

            if (exp.maxFrames > 0 && e.framesAlive >= exp.maxFrames)
            {
                expired = true;
                reason = AttackEntityRemovalReason.ExpiredByFrames;
            }
            else if (exp.maxTimeAlive > 0f && e.timeAlive >= exp.maxTimeAlive)
            {
                expired = true;
                reason = AttackEntityRemovalReason.ExpiredByTime;
            }
            else if (exp.maxDistanceTravelled > 0f && e.distanceTravelled >= exp.maxDistanceTravelled)
            {
                expired = true;
                reason = AttackEntityRemovalReason.ExpiredByDistance;
            }

            if (expired)
            {
                removalEvents.Add(new AttackEntityRemovalEvent
                {
                    entityId = e.entityId,
                    reason = reason
                });
            }
        }
    }
}
