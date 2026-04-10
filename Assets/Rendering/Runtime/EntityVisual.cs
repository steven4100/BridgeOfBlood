public struct EntityVisual
{
    public int frameIndex;
    public float scale;
    public int animationFrameCount;
    public float animationFramesPerSecond;

    public static EntityVisual None => new EntityVisual
    {
        frameIndex = -1,
        scale = 1f,
        animationFrameCount = 1,
        animationFramesPerSecond = 0f
    };
}
