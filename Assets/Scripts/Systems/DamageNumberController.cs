using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Owns the damage number pipeline: spawns from damage events, updates motion/opacity, and renders.
/// Consumes DamageEvents (e.g. from GameSimulation) and drives DamageNumberManager and DamageNumberRenderSystem.
/// </summary>
public class DamageNumberController
{
    private readonly DamageNumberManager _manager;
    private readonly DamageNumberRenderSystem _renderSystem;

    public DamageNumberController(Material material = null)
    {
        _manager = new DamageNumberManager();
        _renderSystem = new DamageNumberRenderSystem(material);
    }

    /// <summary>
    /// Spawns a damage number for each event. Uses enemy array to look up velocity for horizontal drift.
    /// Caller should clear damage events on the simulation after calling this.
    /// </summary>
    public void SpawnFromDamageEvents(NativeArray<DamageEvent> damageEvents, NativeArray<Enemy> enemies)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];
            float velocityX = 0f;
            if (evt.enemyIndex >= 0 && evt.enemyIndex < enemies.Length)
                velocityX = enemies[evt.enemyIndex].moveSpeed;
            _manager.Spawn(evt.position, (int)evt.damageDealt, velocityX: velocityX, isCrit: evt.isCrit);
        }
    }

    /// <summary>Advances damage number lifetimes and motion. Call each frame when time advances.</summary>
    public void Update(float deltaTime)
    {
        _manager.Update(deltaTime);
    }

    /// <summary>Renders all live damage numbers. Call after Update in the game loop.</summary>
    public void Render(RectTransform simZone, Camera camera)
    {
        _renderSystem.Render(_manager.GetEntities(), simZone, camera);
    }

    public void Dispose()
    {
        _manager?.Dispose();
        _renderSystem?.Dispose();
    }
}
