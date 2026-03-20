using Unity.Collections;

/// <summary>
/// Orchestrates effect sprite lifecycle: spawns from damage events, updates lifetimes, exposes live sprites for rendering.
/// </summary>
public class EffectSpriteController
{
    private readonly EffectSpriteManager _manager;

    public EffectSpriteController()
    {
        _manager = new EffectSpriteManager();
    }

    /// <summary>
    /// Spawns on-hit and on-kill effect sprites from this frame's damage events.
    /// Must be called before ClearDamageEvents and before attack entities are removed.
    /// </summary>
    public void SpawnFromDamageEvents(NativeArray<DamageEvent> damageEvents, NativeArray<AttackEntity> attackEntities)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];
            if (evt.attackEntityIndex < 0 || evt.attackEntityIndex >= attackEntities.Length)
                continue;

            AttackEntity atk = attackEntities[evt.attackEntityIndex];

            if (atk.onHitEffect.IsValid)
                _manager.Spawn(evt.position, atk.onHitEffect.visual, atk.onHitEffect.lifetime);

            if (evt.wasKill && atk.onKillEffect.IsValid)
                _manager.Spawn(evt.position, atk.onKillEffect.visual, atk.onKillEffect.lifetime);
        }
    }

    public void Update(float dt) => _manager.Update(dt);

    public NativeArray<EffectSprite> GetEntities() => _manager.GetEntities();

    public void Dispose() => _manager?.Dispose();
}
