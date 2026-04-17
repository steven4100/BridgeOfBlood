using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Multi-frame chaining: when a projectile hits a target, it is redirected to one new target (random in range).
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
    /// For each hit: if the attack has chaining and chainHitsSoFar &lt; chainCount, pick a random neighbor in chain
    /// range; if it is the enemy just hit, remove it from the candidate list and pick again until a different target
    /// is chosen or the list is empty, then redirect the projectile (position at hit, velocity toward that target),
    /// increment chainHitsSoFar. If there is no valid target in range, exhaust remaining chains (chainHitsSoFar =
    /// chainCount) so CollectRemovals later adds it to the remove buffer.
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

            _candidateIndices.Clear();
            grid.QueryNeighbors(hit.hitPosition, policy.chainRange, _candidateIndices);

            var random = Random.CreateFromIndex(1);
            int bestIndex = -1;
            while (_candidateIndices.Length > 0)
            {
                int r = random.NextInt(_candidateIndices.Length);
                int ei = _candidateIndices[r];
                if (ei == hit.enemyIndex)
                {
                    _candidateIndices.RemoveAtSwapBack(r);
                    continue;
                }

                bestIndex = ei;
                break;
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
