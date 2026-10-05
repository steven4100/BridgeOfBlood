using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Collapses all enemies inside a circular region into one stable-slot survivor whose current and
/// maximum health are the corresponding sums. The survivor is moved to the absorbed group centroid.
/// </summary>
public sealed class EnemyCombineMapModifier : MapModifier
{
    public Vector2 Center { get; }
    public float Radius { get; }

    public EnemyCombineMapModifier(Vector2 center, float radius)
    {
        Center = center;
        Radius = Mathf.Max(0f, radius);
    }

    public override IEnumerable<CombatSimWait> Apply(MapModifierContext context)
    {
        EnemyManager manager = context.EnemyManager;
        manager.BuildGrid();

        using var candidates = new NativeList<int>(Allocator.Temp);
        float2 center = new float2(Center.x, Center.y);
        manager.QueryEnemies(center, Radius, candidates);

        EnemyBuffers enemies = manager.GetBuffers();
        float radiusSquared = Radius * Radius;
        int survivorIndex = -1;
        int absorbedCount = 0;
        float totalHealth = 0f;
        float totalMaxHealth = 0f;
        float2 centroidSum = float2.zero;

        for (int i = 0; i < candidates.Length; i++)
        {
            int enemyIndex = candidates[i];
            if (!enemies.IsLive(enemyIndex))
                continue;

            EnemyMotion motion = enemies.Motion[enemyIndex];
            if (math.distancesq(motion.position, center) > radiusSquared)
                continue;

            if (survivorIndex < 0)
                survivorIndex = enemyIndex;

            EnemyVitality vitality = enemies.Vitality[enemyIndex];
            totalHealth += vitality.health;
            totalMaxHealth += vitality.maxHealth;
            centroidSum += motion.position;
            absorbedCount++;
        }

        if (absorbedCount < 2)
            yield break;

        EnemyVitality survivorVitality = enemies.Vitality[survivorIndex];
        survivorVitality.health = totalHealth;
        survivorVitality.maxHealth = totalMaxHealth;
        enemies.Vitality[survivorIndex] = survivorVitality;

        EnemyMotion survivorMotion = enemies.Motion[survivorIndex];
        survivorMotion.position = centroidSum / absorbedCount;
        enemies.Motion[survivorIndex] = survivorMotion;

        for (int i = 0; i < candidates.Length; i++)
        {
            int enemyIndex = candidates[i];
            if (enemyIndex == survivorIndex || !enemies.IsLive(enemyIndex))
                continue;

            EnemyMotion motion = enemies.Motion[enemyIndex];
            if (math.distancesq(motion.position, center) <= radiusSquared)
                manager.Remove(enemies.GetEntityId(enemyIndex));
        }

        manager.BuildGrid();
        yield break;
    }
}
