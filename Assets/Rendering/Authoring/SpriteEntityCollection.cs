using UnityEngine;

[CreateAssetMenu(menuName = "BridgeOfBlood/Sprite Entity Collection")]
public class SpriteEntityCollection : SpriteProvider
{
    public SpriteEntityVisual[] entries;
    public float scale = 1f;

    public override EntityVisual Resolve(uint seed)
    {
        if (entries == null || entries.Length == 0)
            return new EntityVisual
            {
                frameIndex = -1,
                scale = scale,
                animationFrameCount = 1,
                animationFramesPerSecond = 0f
            };

        var entry = entries[seed % (uint)entries.Length];
        if (entry == null)
            return new EntityVisual
            {
                frameIndex = -1,
                scale = scale,
                animationFrameCount = 1,
                animationFramesPerSecond = 0f
            };

        EntityVisual inner = entry.Resolve(seed);
        return new EntityVisual
        {
            frameIndex = inner.frameIndex,
            scale = scale,
            animationFrameCount = inner.animationFrameCount,
            animationFramesPerSecond = inner.animationFramesPerSecond
        };
    }
}
