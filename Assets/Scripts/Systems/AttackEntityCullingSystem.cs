using Unity.Collections;
using UnityEngine;

/// <summary>
/// Collects attack entities that have left the simulation bounds (off screen).
/// Appends removal events so they are removed at end of frame via AttackEntityManager.ApplyRemovals.
/// </summary>
public class AttackEntityCullingSystem
{
    /// <summary>
    /// For each attack entity whose position is outside the given bounds,
    /// appends a removal event with reason CulledOffScreen.
    /// </summary>
    public void CollectRemovals(
        NativeArray<AttackEntity> attackEntities,
        Rect bounds,
        NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        float xMin = bounds.xMin;
        float xMax = bounds.xMax;
        float yMin = bounds.yMin;
        float yMax = bounds.yMax;

        for (int i = 0; i < attackEntities.Length; i++)
        {
            AttackEntity e = attackEntities[i];
            float x = e.position.x;
            float y = e.position.y;

            if (x < xMin || x > xMax || y < yMin || y > yMax)
            {
                removalEvents.Add(new AttackEntityRemovalEvent
                {
                    entityId = e.entityId,
                    reason = AttackEntityRemovalReason.CulledOffScreen
                });
            }
        }
    }
}
