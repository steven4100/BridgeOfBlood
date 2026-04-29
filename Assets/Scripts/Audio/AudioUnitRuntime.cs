[System.Serializable]
public struct AudioUnitRuntime
{
    public const int InvalidClipIndex = -1;

    public int clipIndex;
    public float volume;
    public float pitch;
    public bool playOneShot;

    public bool IsValid => clipIndex >= 0;

    public static AudioUnitRuntime None => new AudioUnitRuntime
    {
        clipIndex = InvalidClipIndex,
        volume = 1f,
        pitch = 1f,
        playOneShot = false
    };
}
