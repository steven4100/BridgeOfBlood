using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "BridgeOfBlood/Sprite Entity Visual")]
public class SpriteEntityVisual : SpriteProvider
{
    public Sprite sprite;
    public float scale = 1f;

    [Tooltip("Number of equal-width frames left-to-right inside the sprite rect. Rebuild atlas after changing.")]
    [Min(1)]
    public int frameCount = 1;

    [Tooltip("Playback rate when frameCount > 1.")]
    public float framesPerSecond = 10f;

    public int bakedFrameIndex = -1;

    public override EntityVisual Resolve(uint seed)
    {
        int count = math.max(1, frameCount);
        return new EntityVisual
        {
            frameIndex = bakedFrameIndex,
            scale = scale,
            animationFrameCount = count,
            animationFramesPerSecond = framesPerSecond
        };
    }
}
