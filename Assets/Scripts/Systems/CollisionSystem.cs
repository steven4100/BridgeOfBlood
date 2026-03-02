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
/// Outputs a flat list of CollisionEvents for downstream processing. Does not update enemiesHit
/// (HitResolver → ChainSystem → DamageSystem; DamageSystem increments enemiesHit when applying hits).
/// Assumes grid was built from the same enemies array; caller (e.g. EnemyManager.ValidateGridForCurrentEnemies) must validate upstream.
/// </summary>
public class CollisionSystem
{
    private NativeList<int> _candidateIndices;

    public CollisionSystem()
    {
        _candidateIndices = new NativeList<int>(64, Allocator.Persistent);
    }

    /// <summary>
    /// Detects all attack-entity-vs-enemy overlaps this frame. Pure geometry; no pierce or other policy.
    /// Call after movement and BuildGrid. Downstream PierceSystem filters by pierce policy.
    /// </summary>
    /// <param name="attackEntities">Read-only for overlap.</param>
    /// <param name="enemies">Read-only enemy array (must match grid ordering from BuildGrid).</param>
    /// <param name="grid">Spatial grid built from the current enemy positions.</param>
    /// <param name="results">Cleared and filled with all overlapping collision events.</param>
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
            float queryRadius = HitBoxQueryRadius(atk);

            _candidateIndices.Clear();
            grid.QueryNeighbors(atk.position, queryRadius, _candidateIndices);

            for (int c = 0; c < _candidateIndices.Length; c++)
            {
                int ei = _candidateIndices[c];

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
