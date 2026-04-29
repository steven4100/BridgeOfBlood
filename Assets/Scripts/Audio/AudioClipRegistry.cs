using System;
using System.Collections.Generic;
using UnityEngine;

public static class AudioClipRegistry
{
    static readonly List<AudioClip> Clips = new List<AudioClip>(64);
    static readonly Dictionary<AudioClip, int> ClipToIndex = new Dictionary<AudioClip, int>(64);

    public static int Register(AudioClip clip)
    {
        if (clip == null)
            return AudioUnitRuntime.InvalidClipIndex;

        if (ClipToIndex.TryGetValue(clip, out int index))
            return index;

        index = Clips.Count;
        Clips.Add(clip);
        ClipToIndex.Add(clip, index);
        return index;
    }

    public static void RegisterAll(AudioUnit[] units)
    {
        if (units == null || units.Length == 0)
            return;

        Array.Sort(units, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
        for (int i = 0; i < units.Length; i++)
        {
            AudioUnit unit = units[i];
            if (unit == null || unit.clip == null)
                continue;
            Register(unit.clip);
        }
    }

    public static int ClipCount => Clips.Count;

    public static AudioClip Get(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= Clips.Count)
            return null;
        return Clips[clipIndex];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Clips.Clear();
        ClipToIndex.Clear();
    }
}
