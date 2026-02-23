using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// A collision between one attack entity and one enemy, produced by CollisionSystem.
/// Downstream systems (damage, telemetry, VFX) consume these without coupling to collision logic.
/// </summary>
public struct CollisionEvent
{
    public int attackEntityId;
    public int attackEntityIndex;
    public int enemyEntityId;
    public int enemyIndex;
    public float2 enemyPosition;
    public float2 attackEntityPosition;
}

/// <summary>
/// Detects overlaps between attack entity hitboxes and enemy positions.
/// Queries the spatial grid for broad-phase, then runs narrow-phase overlap tests.
/// Outputs a flat list of CollisionEvents for downstream processing.
/// Updates enemiesHit on attack entities that score hits.
/// </summary>
public class CollisionSystem
{
    private NativeList<int> _candidateIndices;

    public CollisionSystem()
    {
        _candidateIndices = new NativeList<int>(64, Allocator.Persistent);
    }

    /// <summary>
    /// Detects all attack-entity-vs-enemy overlaps this frame.
    /// Call after movement and BuildGrid, before RemoveExpired.
    /// </summary>
    /// <param name="attackEntities">Read-write: enemiesHit is incremented on hits.</param>
    /// <param name="enemies">Read-only enemy array (must match grid ordering from BuildGrid).</param>
    /// <param name="grid">Spatial grid built from the current enemy positions.</param>
    /// <param name="results">Cleared and filled with collision events.</param>
    public void Detect(
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        GridSpatialPartition grid,
        NativeList<CollisionEvent> results)
    {
        results.Clear();

        for (int ai = 0; ai < attackEntities.Length; ai++)
        {
            AttackEntity atk = attackEntities[ai];

            int hitsRemaining = atk.lifecycle.maxNumEnemiesHit > 0
                ? atk.lifecycle.maxNumEnemiesHit - atk.enemiesHit
                : int.MaxValue;
            if (hitsRemaining <= 0) continue;

            float queryRadius = HitBoxQueryRadius(atk);

            _candidateIndices.Clear();
            grid.QueryNeighbors(atk.position, queryRadius, _candidateIndices);

            int hitsThisFrame = 0;
            for (int c = 0; c < _candidateIndices.Length; c++)
            {
                if (hitsThisFrame >= hitsRemaining) break;

                int ei = _candidateIndices[c];
                if (ei < 0 || ei >= enemies.Length) continue;

                Enemy enemy = enemies[ei];

                if (!Overlaps(atk, enemy.position))
                    continue;

                results.Add(new CollisionEvent
                {
                    attackEntityId = atk.entityId,
                    attackEntityIndex = ai,
                    enemyEntityId = enemy.entityId,
                    enemyIndex = ei,
                    enemyPosition = enemy.position,
                    attackEntityPosition = atk.position
                });

                hitsThisFrame++;
            }

            if (hitsThisFrame > 0)
            {
                atk.enemiesHit += hitsThisFrame;
                attackEntities[ai] = atk;
            }
        }
    }

    static float HitBoxQueryRadius(in AttackEntity atk)
    {
        float scale = atk.currentHitBoxScale;
        if (atk.hitBox.isSphere)
            return atk.hitBox.sphereRadius * scale;
        if (atk.hitBox.isRect)
            return math.length(atk.hitBox.rectDimension * 0.5f) * scale;
        return 0f;
    }

    static bool Overlaps(in AttackEntity atk, float2 point)
    {
        float scale = atk.currentHitBoxScale;

        if (atk.hitBox.isSphere)
        {
            float r = atk.hitBox.sphereRadius * scale;
            float distSq = math.distancesq(atk.position, point);
            return distSq <= r * r;
        }

        if (atk.hitBox.isRect)
        {
            float2 halfExtents = atk.hitBox.rectDimension * 0.5f * scale;
            float2 delta = math.abs(point - atk.position);
            return delta.x <= halfExtents.x && delta.y <= halfExtents.y;
        }

        return false;
    }

    public void Dispose()
    {
        if (_candidateIndices.IsCreated)
            _candidateIndices.Dispose();
    }
}
