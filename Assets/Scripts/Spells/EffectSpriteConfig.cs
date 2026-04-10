using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Shared authoring asset for a time-limited effect sprite (on-hit splash, on-kill explosion, etc.).
/// Multiple AttackEntityData assets can reference the same EffectSpriteConfig.
/// </summary>
[CreateAssetMenu(fileName = "EffectSpriteConfig", menuName = "BridgeOfBlood/Spells/Effect Sprite Config")]
public class EffectSpriteConfig : ScriptableObject
{
    [Tooltip("Sprite visual for this effect. Run Tools > BridgeOfBlood > Rebuild Sprite Rendering Data after assigning.")]
    public SpriteProvider visual;

    [Tooltip("How long the effect sprite persists in seconds.")]
    public float lifetime = 0.3f;

    /// <summary>
    /// Resolves the authoring data into a blittable runtime struct.
    /// </summary>
    public EffectSpriteConfigRuntime ToRuntime(uint seed = 0)
    {
        if (visual == null)
            return EffectSpriteConfigRuntime.Default();

        return new EffectSpriteConfigRuntime
        {
            visual = visual.Resolve(seed),
            lifetime = lifetime
        };
    }
}

/// <summary>
/// Blittable runtime data for a configured effect sprite. Carried on AttackEntity;
/// spawned into EffectSpriteManager when events occur.
/// A frameIndex of -1 means no effect is configured.
/// </summary>
public struct EffectSpriteConfigRuntime
{
    public EntityVisual visual;
    public float lifetime;

    public bool IsValid => visual.frameIndex >= 0 && lifetime > 0f;

    public static EffectSpriteConfigRuntime Default() => new EffectSpriteConfigRuntime
    {
        visual = EntityVisual.None,
        lifetime = 0f
    };
}

/// <summary>
/// A live effect sprite with position, visual, and remaining lifetime.
/// Managed by EffectSpriteManager; rendered alongside enemies and attack entities.
/// Blittable for NativeList storage.
/// </summary>
public struct EffectSprite
{
    public float2 position;
    public EntityVisual visual;
    public float timeAlive;
    public float lifetime;
}
