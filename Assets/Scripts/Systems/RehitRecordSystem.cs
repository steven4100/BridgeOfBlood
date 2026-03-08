using BridgeOfBlood.Data.Enemies;
using Unity.Collections;

/// <summary>
/// Records each hit into the attack entity's rehit list (enemyId, timeAlive) so the next frame's resolver can reject rehits within cooldown.
/// Call after DamageSystem.ProcessHits. Only updates entities with rehitCooldownSeconds > 0.
/// </summary>
public static class RehitRecordSystem
{
    /// <summary>
    /// Records hit events into the corresponding rehit policy lists. When a policy's recentHits is at capacity, evicts the oldest entry (smallest hitTimeAlive) before adding.
    /// </summary>
    public static void RecordRehitHits(
        NativeArray<HitEvent>.ReadOnly hitEvents,
        NativeArray<AttackEntity>.ReadOnly attackEntities,
        NativeArray<Enemy>.ReadOnly enemies,
        NativeArray<RehitPolicyRuntime> rehitPolicies)
    {
        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];
            int ai = hit.attackEntityIndex;
            if (ai < 0 || ai >= rehitPolicies.Length) continue;
            if (hit.enemyIndex < 0 || hit.enemyIndex >= enemies.Length) continue;

            RehitPolicyRuntime rehit = rehitPolicies[ai];
            if (rehit.rehitCooldownSeconds <= 0f) continue;

            AttackEntity atk = attackEntities[ai];
            Enemy enemy = enemies[hit.enemyIndex];
            var entry = new RehitEntry { enemyId = enemy.entityId, hitTimeAlive = atk.timeAlive };

            if (rehit.recentHits.Length >= rehit.recentHits.Capacity)
            {
                int oldestIndex = 0;
                float oldest = rehit.recentHits[0].hitTimeAlive;
                for (int j = 1; j < rehit.recentHits.Length; j++)
                {
                    if (rehit.recentHits[j].hitTimeAlive < oldest)
                    {
                        oldest = rehit.recentHits[j].hitTimeAlive;
                        oldestIndex = j;
                    }
                }
                rehit.recentHits.RemoveAt(oldestIndex);
            }
            rehit.recentHits.Add(entry);
            rehitPolicies[ai] = rehit;
        }
    }
}
