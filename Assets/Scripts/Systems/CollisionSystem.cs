using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// A collision between one attack entity and one enemy, produced by CollisionSystem.
/// Downstream systems (damage, telemetry, VFX) consume these without coupling to collision logic.
/// </summary>
public struct CollisionEvent
{
    public EntityId attackEntityId;
    public EntityId enemyEntityId;
    public float2 enemyPosition;
    public float2 attackEntityPosition;
}

/// <summary>
/// Detects overlaps between attack entity hitboxes and enemy circles.
/// Queries the spatial grid for broad-phase, then runs narrow-phase overlap tests.
/// The query radius includes the largest live enemy collision radius so a circle whose center
/// sits outside the attack hitbox is still tested. A sphere overlaps when the distance is at most
/// the attack radius plus the enemy radius. A rect overlaps when the closest point on the oriented
/// rect is within the enemy radius.
/// Outputs a flat list of CollisionEvents for downstream processing. Does not update enemiesHit
/// (HitResolver → ChainSystem → DamageSystem; DamageSystem increments enemiesHit when applying hits).
/// Assumes grid was built from the same enemy motion column; caller (e.g. EnemyManager.ValidateGridForCurrentEnemies) must validate upstream.
/// </summary>
public class CollisionSystem
{
    private NativeList<int> _candidateIndices;

    public CollisionSystem()
    {
        _candidateIndices = new NativeList<int>(64, Allocator.Persistent);
    }

    public void Detect(
        NativeArray<AttackEntity> attackEntities,
        NativeArray<byte> attackAlive,
        NativeArray<HitBoxRuntime> hitBoxes,
        EnemyBuffers enemies,
        GridSpatialPartition grid,
        float maxEnemyCollisionRadius,
        NativeList<CollisionEvent> results)
    {
        results.Clear();

        for (int ai = 0; ai < attackEntities.Length; ai++)
        {
            if (attackAlive[ai] == 0)
                continue;

            HitBoxRuntime hitBox = hitBoxes[ai];
            if (!hitBox.isActive)
                continue;

            AttackEntity atk = attackEntities[ai];
            float queryRadius = HitBoxQueryRadius(in hitBox) + math.max(maxEnemyCollisionRadius, 0f);
            if (queryRadius <= 0f)
                continue;

            _candidateIndices.Clear();
            grid.QueryNeighbors(atk.position, queryRadius, _candidateIndices);

            for (int c = 0; c < _candidateIndices.Length; c++)
            {
                int ei = _candidateIndices[c];
                if (!enemies.IsLive(ei))
                    continue;

                float2 enemyPos = enemies.Motion[ei].position;

                if (!Overlaps(in atk, in hitBox, enemyPos, enemies.Hurtboxes[ei].radius))
                    continue;

                results.Add(new CollisionEvent
                {
                    attackEntityId = atk.entityId,
                    enemyEntityId = enemies.GetEntityId(ei),
                    enemyPosition = enemyPos,
                    attackEntityPosition = atk.position
                });
            }
        }
    }

    static float HitBoxQueryRadius(in HitBoxRuntime hitBox)
    {
        float scale = hitBox.currentScale;
        if (hitBox.hitBox.isSphere)
            return hitBox.hitBox.sphereRadius * scale;
        if (hitBox.hitBox.isRect)
            return math.length(hitBox.hitBox.rectDimension * 0.5f) * scale;
        return 0f;
    }

    static bool Overlaps(in AttackEntity atk, in HitBoxRuntime hitBox, float2 center, float enemyRadius)
    {
        float scale = hitBox.currentScale;
        float reach = math.max(enemyRadius, 0f);

        if (hitBox.hitBox.isSphere)
        {
            float r = hitBox.hitBox.sphereRadius * scale + reach;
            float distSq = math.distancesq(atk.position, center);
            return distSq <= r * r;
        }

        if (hitBox.hitBox.isRect)
        {
            float2 halfExtents = hitBox.hitBox.rectDimension * 0.5f * scale;
            float2 rel = center - atk.position;
            float2 fwd = atk.direction;
            float lenSq = math.lengthsq(fwd);
            if (lenSq > 1e-12f)
                fwd *= math.rsqrt(lenSq);
            else
                fwd = new float2(1f, 0f);
            float2 perp = new float2(-fwd.y, fwd.x);
            float localX = math.dot(rel, fwd);
            float localY = math.dot(rel, perp);
            float clampedX = math.clamp(localX, -halfExtents.x, halfExtents.x);
            float clampedY = math.clamp(localY, -halfExtents.y, halfExtents.y);
            float dx = localX - clampedX;
            float dy = localY - clampedY;
            return dx * dx + dy * dy <= reach * reach;
        }

        return false;
    }

    public void Dispose()
    {
        if (_candidateIndices.IsCreated)
            _candidateIndices.Dispose();
    }
}
