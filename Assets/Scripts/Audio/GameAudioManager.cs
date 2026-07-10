using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
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

    [SerializeField] int maxPlaysPerFrame = 1;
    [SerializeField] int maxQueueLengthPerClip = 1;
    [SerializeField] bool preloadFromResources = true;
    [SerializeField] float spatialBlend = 0f;

    readonly Dictionary<int, Queue<AudioPlayRequest>> _queueByClipIndex = new Dictionary<int, Queue<AudioPlayRequest>>(64);
    readonly Dictionary<int, AudioSource> _voiceByClipIndex = new Dictionary<int, AudioSource>(64);

    void Awake()
    {
        if (preloadFromResources)
            AudioClipRegistry.RegisterAll(Resources.LoadAll<AudioUnit>(string.Empty));

        int clipCount = AudioClipRegistry.ClipCount;
        for (int i = 0; i < clipCount; i++)
            GetOrCreateVoice(i);
    }

    void OnEnable()
    {
        SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
        SharedGameEventBus.Bus.SubscribeTo<SpellCastEvent>(OnSpellCast);
    }

    void OnDisable()
    {
        SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
        SharedGameEventBus.Bus.UnsubscribeFrom<SpellCastEvent>(OnSpellCast);
    }

    void OnSimulationComplete(ref SimulationCompleteEvent @event)
    {
        EnqueueFromCombatEvents(
            @event.simulationState.DamageEvents,
            @event.simulationState.KillEvents);
        UpdateDrain();
    }

    void OnSpellCast(ref SpellCastEvent @event)
    {
        SpellCastResult castResult = @event.castResult;
        IReadOnlyList<RuntimeSpell> spells = @event.spells;
        for (int i = 0; i < spells.Count; i++)
        {
            RuntimeSpell spell = spells[i];
            if (spell.spellId != castResult.spellId)
                continue;

            AudioUnit unit = spell.Definition.castAudio;
            if (unit != null)
            {
                uint seed = AttackEntityBuildRngSeed.Mix(
                    castResult.spellId,
                    castResult.invocationCount,
                    0,
                    0x41A3F5C);
                RequestOneShot(unit.ToRuntime(seed), @event.origin);
            }
            return;
        }
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
        int budgetPerQueue = Mathf.Max(1, maxPlaysPerFrame);
        foreach (KeyValuePair<int, Queue<AudioPlayRequest>> entry in _queueByClipIndex)
        {
            Queue<AudioPlayRequest> queue = entry.Value;
            for (int i = 0; i < budgetPerQueue && queue.Count > 0; i++)
                PlayRequest(queue.Dequeue());
        }
    }

    void PlayRequest(in AudioPlayRequest request)
    {
        AudioClip clip = AudioClipRegistry.Get(request.unit.clipIndex);
        if (clip == null)
            return;

        AudioSource source = GetOrCreateVoice(request.unit.clipIndex);
        //source.transform.position = request.worldPosition;
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

    void Enqueue(in AudioUnitRuntime unit, Vector3 worldPosition)
    {
        Queue<AudioPlayRequest> queue = GetOrCreateQueue(unit.clipIndex);

        if (queue.Count >= maxQueueLengthPerClip)
            return;

        int cap = Mathf.Max(1, maxQueueLengthPerClip);
        while (queue.Count >= cap)
            queue.Dequeue();

        queue.Enqueue(new AudioPlayRequest
        {
            unit = unit,
            worldPosition = worldPosition
        });
    }

    Queue<AudioPlayRequest> GetOrCreateQueue(int clipIndex)
    {
        if (_queueByClipIndex.TryGetValue(clipIndex, out Queue<AudioPlayRequest> existing))
            return existing;

        int cap = Mathf.Max(1, maxQueueLengthPerClip);
        var queue = new Queue<AudioPlayRequest>(cap);
        _queueByClipIndex.Add(clipIndex, queue);
        return queue;
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
