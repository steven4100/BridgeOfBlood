using Unity.Collections;

/// <summary>
/// Collects removal events when an attack entity has hit its pierce limit (maxEnemiesHit).
/// Pierce filtering is done in HitResolver so the pipeline is Detect → Resolve → Chain → Damage.
/// </summary>
public class PierceSystem
{
    /// <summary>
    /// For each attack entity where enemiesHit >= piercePolicy.maxEnemiesHit (and maxEnemiesHit > 0),
    /// appends a removal event. Call after damage has been applied (enemiesHit is current).
    /// </summary>
    public void CollectRemovals(
        NativeArray<AttackEntity> attackEntities,
        NativeArray<PiercePolicyRuntime> piercePolicies,
        NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        for (int i = 0; i < attackEntities.Length; i++)
        {
            AttackEntity e = attackEntities[i];
            PiercePolicyRuntime pierce = piercePolicies[i];
            if (!pierce.isActive) continue;
            if (pierce.maxEnemiesHit > 0 && e.enemiesHit >= pierce.maxEnemiesHit)
            {
                removalEvents.Add(new AttackEntityRemovalEvent
                {
                    entityId = e.entityId,
                    reason = AttackEntityRemovalReason.PierceLimitReached
                });
            }
        }
    }
}
