using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Collects enemies that have left the simulation bounds (e.g. past the right edge).
/// </summary>
public class EnemyCullingSystem
{
    /// <summary>
    /// Fills the list with entity IDs of enemies whose position.x is greater than rightEdgeX.
    /// Clears the list before adding. Caller should pass the result to EnemyManager.RemoveEnemies.
    /// </summary>
    public void CollectEnemiesPastRightEdge(NativeArray<Enemy> enemies, float rightEdgeX, List<int> outIdsToRemove)
    {
        outIdsToRemove.Clear();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].position.x > rightEdgeX)
                outIdsToRemove.Add(enemies[i].entityId);
        }
    }
}
