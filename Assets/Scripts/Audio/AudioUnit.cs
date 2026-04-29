using BridgeOfBlood.Data.Shared;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "AudioUnit", menuName = "BridgeOfBlood/Audio/Audio Unit")]
public class AudioUnit : ScriptableObject
{
    public AudioClip clip;
    public FloatRange volumeRange = new FloatRange { min = 1f, max = 1f };
    public FloatRange pitchRange = new FloatRange { min = 1f, max = 1f };

    [Tooltip("If true, uses AudioSource.PlayOneShot so overlapping instances can stack. If false, one dedicated voice per clip restarts on repeat.")]
    [FormerlySerializedAs("playOneShot")]
    public bool usePlayOneShot;

    public AudioUnitRuntime ToRuntime(uint seed)
    {
        if (clip == null)
            return AudioUnitRuntime.None;

        int clipIndex = AudioClipRegistry.Register(clip);
        if (clipIndex < 0)
            return AudioUnitRuntime.None;

        uint runtimeSeed = seed == 0u ? 1u : seed;
        var rng = Unity.Mathematics.Random.CreateFromIndex(runtimeSeed);
        FloatRange volume = volumeRange;
        FloatRange pitch = pitchRange;
        volume.ClampOrder();
        pitch.ClampOrder();

        return new AudioUnitRuntime
        {
            clipIndex = clipIndex,
            volume = Mathf.Max(0f, volume.ResolveUniform(ref rng)),
            pitch = Mathf.Clamp(pitch.ResolveUniform(ref rng), 0.1f, 3f),
            playOneShot = usePlayOneShot
        };
    }

    void OnValidate()
    {
        volumeRange.ClampOrder();
        pitchRange.ClampOrder();
        volumeRange.min = Mathf.Max(0f, volumeRange.min);
        volumeRange.max = Mathf.Max(0f, volumeRange.max);
        pitchRange.min = Mathf.Clamp(pitchRange.min, 0.1f, 3f);
        pitchRange.max = Mathf.Clamp(pitchRange.max, 0.1f, 3f);
    }
}
