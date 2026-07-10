using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using UnityEngine;

public sealed class KillBurstVfxController : EnemyPositionVfxController
{
    [SerializeField] int numParticles = 10;
    [SerializeField] float particleSize = 10f;
    [SerializeField, Range(0f, 100f)] float killBurstPercent = 100f;

    int _numParticlesId;
    int _particleSizeId;

    protected override void Awake()
    {
        base.Awake();

        _numParticlesId = Shader.PropertyToID("numParticles");
        _particleSizeId = Shader.PropertyToID("particleSize");

        effect.SetInt(_numParticlesId, numParticles);
        effect.SetFloat(_particleSizeId, particleSize);
    }

    void OnEnable()
    {
        SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
    }

    protected override void OnDisable()
    {
        SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
        base.OnDisable();
    }

    void OnSimulationComplete(ref SimulationCompleteEvent @event)
    {
        SpawnFromKillEvents(@event.simulationState.KillEvents);
    }

    public void SpawnFromKillEvents(NativeArray<EnemyKilledEvent> killEvents)
    {
        if (killEvents.Length == 0 || killBurstPercent <= 0f)
            return;

        bool filterKills = killBurstPercent < 100f;
        float inclusionThreshold = killBurstPercent * 0.01f;

        EnsureCpuCapacity(killEvents.Length);

        int selectedCount = 0;
        for (int i = 0; i < killEvents.Length; i++)
        {
            if (filterKills && Random.value > inclusionThreshold)
                continue;

            var position = killEvents[i].position;
            _positions[selectedCount++] = new Vector3(position.x, 0, position.y);
        }

        if (selectedCount == 0)
            return;

        UploadAndPlay(selectedCount);
    }
}
