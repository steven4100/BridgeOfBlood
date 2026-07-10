using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class IgnitedEnemyVfxController : EnemyPositionVfxController
{
    protected override void Awake()
    {
        base.Awake();
    }

    void OnEnable()
    {
        SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
        UploadPositions(0);
        effect.Play();
    }

    protected override void OnDisable()
    {
        SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
        base.OnDisable();
    }

    void OnSimulationComplete(ref SimulationCompleteEvent @event)
    {
        if (!@event.simulationAdvanced)
            return;

        UploadIgnitedEnemyPositions(@event.simulationState.EnemyBuffers);
    }

    void UploadIgnitedEnemyPositions(EnemyBuffers enemies)
    {
        EnsureCpuCapacity(enemies.AliveCount);

        int count = 0;
        for (int i = 0; i < enemies.SlotCount; i++)
        {
            if (!enemies.IsLive(i))
                continue;

            if ((enemies.Status[i] & StatusAilmentFlag.Ignited) == 0)
                continue;

            var position = enemies.Motion[i].position;
            _positions[count++] = new Vector3(position.x, position.y, 0);
        }

        UploadPositions(count);
    }
}
