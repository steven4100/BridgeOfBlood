using BridgeOfBlood.Data.Enemies;
using System.Collections.Generic;
using Unity.Collections;

/// <summary>
/// Scans enemies and collects the entity ids of any with health at or below zero.
/// Call after DamageSystem so dead enemies are cleaned up before the next frame.
/// </summary>
public class DeadEnemyRemovalSystem
{
    public void CollectDeadEnemies(NativeArray<Enemy> enemies, List<int> deadIds)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].health <= 0f)
                deadIds.Add(enemies[i].entityId);
        }
    }
}
