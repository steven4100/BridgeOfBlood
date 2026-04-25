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

    public void SpawnFromDamageEvents(NativeArray<DamageEvent> damageEvents, EnemyBuffers enemies)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];
            float velocityX = 0f;
            if (evt.enemyIndex >= 0 && evt.enemyIndex < enemies.Length)
                velocityX = enemies.Motion[evt.enemyIndex].moveSpeed;
            _manager.Spawn(evt.position, (int)evt.damageDealt, velocityX: velocityX, isCrit: evt.isCrit);
        }
    }

    public void SpawnFromTickDamageEvents(NativeArray<TickDamageEvent> tickEvents, EnemyBuffers enemies)
    {
        for (int i = 0; i < tickEvents.Length; i++)
        {
            TickDamageEvent evt = tickEvents[i];
            float velocityX = 0f;
            if (evt.enemyIndex >= 0 && evt.enemyIndex < enemies.Length)
                velocityX = enemies.Motion[evt.enemyIndex].moveSpeed;
            _manager.Spawn(evt.position, (int)evt.damageDealt, velocityX: velocityX, isCrit: false);
        }
    }

    public void Update(float deltaTime)
    {
        _manager.Update(deltaTime);
    }

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
