using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Effect splashes from combat events: NativeList-backed storage, spawn from damage events,
/// ticks lifetimes each frame, exposes sprites for rendering.
/// </summary>
public class EffectSpriteController
{
    private NativeList<EffectSprite> _sprites;

    public EffectSpriteController(int initialCapacity = 256)
    {
        _sprites = new NativeList<EffectSprite>(initialCapacity, Allocator.Persistent);
    }

    /// <summary>
    /// Spawns on-hit and on-kill effect sprites from this frame's damage events.
    /// Uses per-event VFX snapshots so effects still spawn after the attack entity row is removed (e.g. single-frame expiration).
    /// </summary>
    public void SpawnFromDamageEvents(NativeArray<DamageEvent> damageEvents)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];

            if (evt.onHitEffectForVfx.IsValid)
                Spawn(evt.position, evt.onHitEffectForVfx.visual, evt.onHitEffectForVfx.lifetime);

            if (evt.wasKill && evt.onKillEffectForVfx.IsValid)
                Spawn(evt.position, evt.onKillEffectForVfx.visual, evt.onKillEffectForVfx.lifetime);
        }
    }

    private void Spawn(float2 position, EntityVisual visual, float lifetime)
    {
        if (visual.frameIndex < 0 || lifetime <= 0f) return;

        _sprites.Add(new EffectSprite
        {
            position = position,
            visual = visual,
            timeAlive = 0f,
            lifetime = lifetime
        });
    }

    public void Update(float dt)
    {
        for (int i = _sprites.Length - 1; i >= 0; i--)
        {
            EffectSprite s = _sprites[i];
            s.timeAlive += dt;

            if (s.timeAlive >= s.lifetime)
            {
                _sprites.RemoveAtSwapBack(i);
                continue;
            }

            _sprites[i] = s;
        }
    }

    public NativeArray<EffectSprite> GetEntities() => _sprites.AsArray();

    public int Count => _sprites.Length;

    public void Clear() => _sprites.Clear();

    public void Dispose()
    {
        if (_sprites.IsCreated)
            _sprites.Dispose();
    }
}
