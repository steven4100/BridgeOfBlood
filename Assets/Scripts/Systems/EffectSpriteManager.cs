using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Manages live effect sprites (on-hit splashes, on-kill explosions).
/// Spawns from combat events, ticks lifetimes each frame, removes expired entries.
/// NativeList-backed, no MonoBehaviours.
/// </summary>
public class EffectSpriteManager
{
    private NativeList<EffectSprite> _sprites;

    public EffectSpriteManager(int initialCapacity = 256)
    {
        _sprites = new NativeList<EffectSprite>(initialCapacity, Allocator.Persistent);
    }

    public void Spawn(float2 position, EntityVisual visual, float lifetime)
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
