using UnityEngine;

[CreateAssetMenu(menuName = "BridgeOfBlood/Sprite Entity Visual")]
public class SpriteEntityVisual : SpriteProvider
{
    public Sprite sprite;
    public float scale = 1f;

    public int bakedFrameIndex = -1;

    public override EntityVisual Resolve(uint seed)
    {
        return new EntityVisual
        {
            frameIndex = bakedFrameIndex,
            scale = scale
        };
    }
}
