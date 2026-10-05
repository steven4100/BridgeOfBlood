using System;
using BridgeOfBlood.Data.Enemies;
using Unity.Mathematics;

/// <summary>
/// A stateful condition the combat simulation can fast-forward until it is true.
/// </summary>
public sealed class CombatSimWait
{
    readonly Func<GameSimulation, bool> _isSatisfied;
    readonly int _requiredTicks;
    int _tickedCount;

    CombatSimWait(Func<GameSimulation, bool> isSatisfied, int requiredTicks = 0)
    {
        _isSatisfied = isSatisfied;
        _requiredTicks = requiredTicks;
    }

    public bool IsSatisfied(GameSimulation simulation)
    {
        if (_tickedCount < _requiredTicks)
            return false;
        return _isSatisfied(simulation);
    }

    public void NotifyTicked()
    {
        _tickedCount++;
    }

    public static CombatSimWait OneTick()
    {
        return new CombatSimWait(_ => true, requiredTicks: 1);
    }

    public static CombatSimWait AttackEntitiesDespawned()
    {
        return new CombatSimWait(simulation => simulation.State.AttackEntityCount == 0);
    }

    public static CombatSimWait KnockbackSettled(float speedEpsilon = 0.05f)
    {
        float epsilonSq = speedEpsilon * speedEpsilon;
        return new CombatSimWait(simulation =>
        {
            EnemyBuffers enemies = simulation.State.EnemyBuffers;
            for (int i = 0; i < enemies.SlotCount; i++)
            {
                if (!enemies.IsLive(i))
                    continue;
                if (math.lengthsq(enemies.Motion[i].knockbackVelocity) > epsilonSq)
                    return false;
            }

            return true;
        });
    }
}

/// <summary>
/// Fast-forwards <see cref="GameSimulation"/> at a fixed timestep until a wait is satisfied.
/// </summary>
public static class CombatSimWaitRunner
{
    public const float DefaultFixedDeltaTime = 1f / 60f;
    public const int DefaultMaxTicks = 480;

    public static void StepOnce(GameSimulation simulation, float deltaTime)
    {
        simulation.AdvanceTime(deltaTime);
        for (int i = 0; i < simulation.StepCount; i++)
            simulation.ExecuteStep(i);
    }

    public static void Run(
        GameSimulation simulation,
        CombatSimWait wait,
        float fixedDeltaTime = DefaultFixedDeltaTime,
        int maxTicks = DefaultMaxTicks)
    {
        int ticks = 0;
        while (!wait.IsSatisfied(simulation))
        {
            if (ticks >= maxTicks)
            {
                throw new InvalidOperationException(
                    $"CombatSimWait did not complete within {maxTicks} simulation ticks.");
            }

            StepOnce(simulation, fixedDeltaTime);
            simulation.ClearFrameCombatEvents();
            wait.NotifyTicked();
            ticks++;
        }
    }
}
