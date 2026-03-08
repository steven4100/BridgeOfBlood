using UnityEngine;

[CreateAssetMenu(menuName = "BridgeOfBlood/Sprite Entity Collection")]
public class SpriteEntityCollection : SpriteProvider
{
    public SpriteEntityVisual[] entries;
    public float scale = 1f;

    public override EntityVisual Resolve(uint seed)
    {
        if (entries == null || entries.Length == 0)
            return new EntityVisual { frameIndex = -1, scale = scale };

        var entry = entries[seed % (uint)entries.Length];
        if (entry == null)
            return new EntityVisual { frameIndex = -1, scale = scale };

        return new EntityVisual
        {
            frameIndex = entry.bakedFrameIndex,
            scale = scale
        };
    }
}
