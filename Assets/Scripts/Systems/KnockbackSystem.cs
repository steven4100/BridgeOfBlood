using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Applies attack knockback to enemies from committed damage events.
/// Facing knockback pushes along the attack's direction. Otherwise the push is radial
/// from the attack center, then opposite velocity, then left.
/// </summary>
public class KnockbackSystem
{
    static readonly float KnockbackDirectionEpsilonSq = 1e-12f;

    public void Apply(
        NativeArray<DamageEvent>.ReadOnly damageEvents,
        NativeArray<AttackEntity>.ReadOnly attackEntities,
        NativeArray<KnockbackRuntime>.ReadOnly knockbacks,
        EnemyBuffers enemies)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent ev = damageEvents[i];
            if (ev.damageDealt <= 0f)
                continue;

            int ai = ev.attackEntityId.Index;
            KnockbackRuntime knockback = knockbacks[ai];
            if (!knockback.isActive || knockback.strength <= 0f)
                continue;

            int ei = ev.enemyEntityId.Index;
            if (!enemies.IsValid(ev.enemyEntityId))
                continue;

            AttackEntity atk = attackEntities[ai];
            EnemyMotion motion = enemies.Motion[ei];

            float2 delta = motion.position - atk.position;
            float2 dir;
            if (knockback.useEntityDirection && math.lengthsq(atk.direction) > KnockbackDirectionEpsilonSq)
                dir = math.normalize(atk.direction);
            else if (math.lengthsq(delta) > KnockbackDirectionEpsilonSq)
                dir = math.normalize(delta);
            else if (math.lengthsq(atk.velocity) > KnockbackDirectionEpsilonSq)
                dir = math.normalize(-atk.velocity);
            else
                dir = new float2(-1f, 0f);

            motion.knockbackVelocity += dir * knockback.strength;
            enemies.Motion[ei] = motion;
        }
    }
}
