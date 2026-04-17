using BridgeOfBlood.Data.Enemies;
using Unity.Collections;

/// <summary>
/// Scans enemies in index order and appends removal pairs for any with health at or below zero (indices ascending).
/// Call after DamageSystem so dead enemies are cleaned up before the next frame.
/// </summary>
public class DeadEnemyRemovalSystem
{
    public void CollectDeadEnemies(NativeArray<Enemy> enemies, NativeList<int> outIndices, NativeList<int> outEntityIds)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].health <= 0f)
            {
                Enemy e = enemies[i];
                outIndices.Add(i);
                outEntityIds.Add(e.entityId);
            }
        }
    }
}
