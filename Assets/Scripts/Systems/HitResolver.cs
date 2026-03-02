using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Resolves collisions into hit information. Pierce policy is applied here so the pipeline is
/// Detect → Resolve → Chain → Damage with no separate pierce step.
/// No side effects—no damage, no telemetry, no enemiesHit update.
/// Outputs HitEvents for ChainSystem and DamageSystem to consume.
/// Assumes collision indices are valid; caller (e.g. AttackEntityManager / pipeline owner) must validate upstream.
/// </summary>
public class HitResolver
{
    /// <summary>
    /// Converts each collision into one HitEvent when allowed by pierce policy. Clears and fills the hitEvents list.
    /// Only collisions that pass the per-entity pierce limit (maxEnemiesHit - enemiesHit this frame) produce hits.
    /// If piercePolicies is default or empty, all collisions become hits. Does not modify enemies or attack entities.
    /// </summary>
    public void Resolve(
        NativeList<CollisionEvent> collisions,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        NativeArray<PiercePolicyRuntime> piercePolicies,
        NativeList<HitEvent> hitEvents)
    {
        hitEvents.Clear();
        if (collisions.Length == 0) return;

        bool applyPierce = piercePolicies.IsCreated && piercePolicies.Length > 0 && attackEntities.Length > 0;
        NativeArray<int> countPerEntity = default;
        if (applyPierce && piercePolicies.Length == attackEntities.Length)
        {
            countPerEntity = new NativeArray<int>(attackEntities.Length, Allocator.Temp);
        }

        try
        {
            for (int c = 0; c < collisions.Length; c++)
            {
                CollisionEvent col = collisions[c];
                int ai = col.attackEntityIndex;

                if (applyPierce && countPerEntity.IsCreated)
                {
                    AttackEntity atk = attackEntities[ai];
                    PiercePolicyRuntime pierce = piercePolicies[ai];
                    int hitsRemaining = int.MaxValue;
                    if (pierce.isActive && pierce.maxEnemiesHit > 0)
                        hitsRemaining = pierce.maxEnemiesHit - atk.enemiesHit;
                    if (hitsRemaining <= 0) continue;
                    if (countPerEntity[ai] >= hitsRemaining) continue;
                    countPerEntity[ai]++;
                }

                hitEvents.Add(new HitEvent
                {
                    attackEntityIndex = col.attackEntityIndex,
                    enemyIndex = col.enemyIndex,
                    hitPosition = col.enemyPosition
                });
            }
        }
        finally
        {
            if (countPerEntity.IsCreated)
                countPerEntity.Dispose();
        }
    }
}
