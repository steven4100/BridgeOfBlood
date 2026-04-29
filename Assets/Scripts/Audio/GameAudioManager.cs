using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    struct AudioPlayRequest
    {
        public AudioUnitRuntime unit;
        public Vector3 worldPosition;
    }

    [SerializeField] int maxPlaysPerFrame = 6;
    [SerializeField] bool preloadFromResources = true;
    [SerializeField] float spatialBlend = 0f;

    readonly Queue<AudioPlayRequest> _queue = new Queue<AudioPlayRequest>(256);
    readonly Dictionary<int, AudioSource> _voiceByClipIndex = new Dictionary<int, AudioSource>(64);

    void Awake()
    {
        if (preloadFromResources)
            AudioClipRegistry.RegisterAll(Resources.LoadAll<AudioUnit>(string.Empty));

        int clipCount = AudioClipRegistry.ClipCount;
        for (int i = 0; i < clipCount; i++)
            GetOrCreateVoice(i);
    }

    public void EnqueueFromCombatEvents(NativeArray<DamageEvent> damageEvents, NativeArray<EnemyKilledEvent> killEvents)
    {
        for (int i = 0; i < damageEvents.Length; i++)
        {
            DamageEvent evt = damageEvents[i];
            if (!evt.onDamageSound.IsValid)
                continue;

            Enqueue(evt.onDamageSound, new Vector3(evt.position.x, evt.position.y, 0f));
        }

        for (int i = 0; i < killEvents.Length; i++)
        {
            EnemyKilledEvent evt = killEvents[i];
            if (!evt.onDeathSound.IsValid)
                continue;

            Enqueue(evt.onDeathSound, new Vector3(evt.position.x, evt.position.y, 0f));
        }
    }

    public void RequestOneShot(in AudioUnitRuntime unit, float2 simulationPosition)
    {
        if (!unit.IsValid)
            return;

        Enqueue(unit, new Vector3(simulationPosition.x, simulationPosition.y, 0f));
    }

    public void RequestOneShot(in AudioUnitRuntime unit)
    {
        if (!unit.IsValid)
            return;

        Enqueue(unit, transform.position);
    }

    void Update()
    {
        UpdateDrain();
    }

    public void UpdateDrain()
    {
        int budget = Mathf.Max(1, maxPlaysPerFrame);
        for (int i = 0; i < budget && _queue.Count > 0; i++)
        {
            AudioPlayRequest request = _queue.Dequeue();
            AudioClip clip = AudioClipRegistry.Get(request.unit.clipIndex);
            if (clip == null)
                continue;

            AudioSource source = GetOrCreateVoice(request.unit.clipIndex);
            source.transform.position = request.worldPosition;
            source.pitch = request.unit.pitch;

            if (request.unit.playOneShot)
            {
                source.volume = 1f;
                source.PlayOneShot(clip, request.unit.volume);
            }
            else
            {
                source.clip = clip;
                source.volume = request.unit.volume;
                source.Play();
            }
        }
    }

    void Enqueue(in AudioUnitRuntime unit, Vector3 worldPosition)
    {
        _queue.Enqueue(new AudioPlayRequest
        {
            unit = unit,
            worldPosition = worldPosition
        });
    }

    AudioSource GetOrCreateVoice(int clipIndex)
    {
        if (_voiceByClipIndex.TryGetValue(clipIndex, out AudioSource existing))
            return existing;

        var source = gameObject.AddComponent<AudioSource>();
        ConfigureVoice(source);
        _voiceByClipIndex.Add(clipIndex, source);
        return source;
    }

    void ConfigureVoice(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
    }
}
