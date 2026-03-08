using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Multi-frame chaining: when a projectile hits a target, it is redirected to one new target (nearest in range).
/// That hit happens in a later frame; redirect repeats until chainCount. Does not append HitEvents—only
/// updates attack entity position/velocity and ChainPolicyRuntime state. No damage or telemetry.
/// Assumes hit events and grid data are valid; caller (e.g. AttackEntityManager.ValidateHitEvents, EnemyManager.ValidateGridForCurrentEnemies) must validate upstream.
/// </summary>
public class ChainSystem
{
    private NativeList<int> _candidateIndices;

    public ChainSystem()
    {
        _candidateIndices = new NativeList<int>(64, Allocator.Persistent);
    }

    /// <summary>
    /// For each hit: if the attack has chaining and chainHitsSoFar &lt; chainCount, add this enemy to hitEnemyIds,
    /// find one next target (nearest in range, excluding previously hit), redirect the projectile (position at hit,
    /// velocity toward next target), increment chainHitsSoFar. If there is no valid target in range, exhaust remaining
    /// chains (chainHitsSoFar = chainCount) so CollectRemovals later adds it to the remove buffer.
    /// </summary>
    public void ResolveChains(
        NativeArray<HitEvent>.ReadOnly hitEvents,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<ChainPolicyRuntime> chainPolicies,
        GridSpatialPartition grid,
        NativeArray<Enemy>.ReadOnly enemies)
    {
        if (attackEntities.Length == 0 || chainPolicies.Length == 0 || enemies.Length == 0)
            return;

        for (int h = 0; h < hitEvents.Length; h++)
        {
            HitEvent hit = hitEvents[h];

            ChainPolicyRuntime policy = chainPolicies[hit.attackEntityIndex];
            if (!policy.isActive || !policy.enabled || policy.chainHitsSoFar >= policy.chainCount || policy.chainRange <= 0f)
                continue;

            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            int currentEnemyId = enemies[hit.enemyIndex].entityId;
            if (policy.excludePreviouslyHit && !policy.hitEnemyIds.Contains(currentEnemyId))
            {
                AddHitEnemyIdDropOldest(ref policy.hitEnemyIds, currentEnemyId, ChainPolicyConstants.MaxPreviouslyHitIds);
            }

            _candidateIndices.Clear();
            grid.QueryNeighbors(hit.hitPosition, policy.chainRange, _candidateIndices);

            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < _candidateIndices.Length; i++)
            {
                int ei = _candidateIndices[i];

                if (policy.excludePreviouslyHit)
                {
                    int eid = enemies[ei].entityId;
                    if (policy.hitEnemyIds.Contains(eid)) continue;
                }

                float2 pos = enemies[ei].position;
                float distSq = math.distancesq(hit.hitPosition, pos);
                if (distSq > policy.chainRange * policy.chainRange) continue;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = ei;
                }
            }

            if (bestIndex < 0)
            {
                policy.chainHitsSoFar = policy.chainCount;
                chainPolicies[hit.attackEntityIndex] = policy;
                continue;
            }

            float2 nextPos = enemies[bestIndex].position;
            float2 dir = math.normalize(nextPos - hit.hitPosition);
            float speed = math.length(atk.velocity);
            if (speed < 0.0001f) speed = 1f;
            atk.velocity = dir * speed;
            atk.position = hit.hitPosition;

            policy.chainHitsSoFar++;

            attackEntities[hit.attackEntityIndex] = atk;
            chainPolicies[hit.attackEntityIndex] = policy;
        }
    }

    /// <summary>
    /// Adds enemyId to the list for ExcludePreviouslyHit. If at capacity, removes the oldest so chaining can continue indefinitely.
    /// Uses the list's actual Capacity (FixedList32Bytes&lt;int&gt; is 7, not 8) so we never exceed it.
    /// </summary>
    static void AddHitEnemyIdDropOldest(ref FixedList32Bytes<int> list, int enemyId, int maxCount)
    {
        if (list.Length >= list.Capacity)
            list.RemoveAt(0);
        list.Add(enemyId);
    }

    /// <summary>
    /// Appends removal events for projectiles with chaining enabled that have exhausted their chains
    /// (chainHitsSoFar >= chainCount). Call in the same phase as pierce/expiration removal collection.
    /// </summary>
    public void CollectRemovals(
        NativeArray<AttackEntity> attackEntities,
        NativeArray<ChainPolicyRuntime> chainPolicies,
        NativeList<AttackEntityRemovalEvent> removalEvents)
    {
        for (int i = 0; i < attackEntities.Length; i++)
        {
            ChainPolicyRuntime policy = chainPolicies[i];
            if (!policy.isActive || !policy.enabled || policy.chainCount <= 0) continue;
            if (policy.chainHitsSoFar < policy.chainCount) continue;

            removalEvents.Add(new AttackEntityRemovalEvent
            {
                entityId = attackEntities[i].entityId,
                reason = AttackEntityRemovalReason.ChainLimitReached
            });
        }
    }

    public void Dispose()
    {
        if (_candidateIndices.IsCreated)
            _candidateIndices.Dispose();
    }
}
