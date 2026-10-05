using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Lightweight one-shot attack-entity spawn used by placed map effects. It intentionally bypasses
/// spell invocation, mana, cast timing, and frame spell modifications.
/// </summary>
public sealed class AttackEntitySpawnMapModifier : MapModifier
{
    readonly MapModifierSettleStep[] _settleSteps;

    public AttackEntityData AttackEntityData { get; }
    public IAttackEntityEmitter Emitter { get; }

    public AttackEntitySpawnMapModifier(
        AttackEntityData attackEntityData,
        IAttackEntityEmitter emitter,
        MapModifierSettleStep[] settleSteps)
    {
        AttackEntityData = attackEntityData;
        Emitter = emitter;
        _settleSteps = settleSteps ?? Array.Empty<MapModifierSettleStep>();
    }

    public override IEnumerable<CombatSimWait> Apply(MapModifierContext context)
    {
        Emitter.Emit(context, AttackEntityData);
        for (int i = 0; i < _settleSteps.Length; i++)
            yield return CreateWait(_settleSteps[i]);
    }

    static CombatSimWait CreateWait(MapModifierSettleStep step)
    {
        switch (step)
        {
            case MapModifierSettleStep.OneTick:
                return CombatSimWait.OneTick();
            case MapModifierSettleStep.AttackEntitiesDespawned:
                return CombatSimWait.AttackEntitiesDespawned();
            case MapModifierSettleStep.KnockbackSettled:
                return CombatSimWait.KnockbackSettled();
            default:
                throw new ArgumentOutOfRangeException(nameof(step), step, null);
        }
    }
}

public interface IAttackEntityEmitter
{
    Vector2 Position { get; }
    void Emit(MapModifierContext context, AttackEntityData attackEntityData);
}

public sealed class PointAttackEntityEmitter : IAttackEntityEmitter
{
    public Vector2 Position { get; }

    public PointAttackEntityEmitter(Vector2 position)
    {
        Position = position;
    }

    public void Emit(MapModifierContext context, AttackEntityData attackEntityData)
    {
        Vector2 authoredVelocity = attackEntityData.entityVelocity;
        Spawn(
            context,
            attackEntityData,
            new float2(Position.x, Position.y),
            new float2(authoredVelocity.x, authoredVelocity.y));
    }

    static void Spawn(
        MapModifierContext context,
        AttackEntityData attackEntityData,
        float2 position,
        float2 velocity)
    {
        var buildContext = new AttackEntityBuildContext(
            attackEntityData,
            spellId: 0,
            spellInvocationId: 0,
            keyframeIndex: 0,
            modifications: null,
            attributeMask: SpellAttributeMask.None,
            position: position,
            velocity: velocity);
        context.AttackEntityManager.Spawn(in buildContext);
    }
}

public sealed class DirectedAttackEntityEmitter : IAttackEntityEmitter
{
    public Vector2 Position { get; }
    public Vector2 Direction { get; }

    public DirectedAttackEntityEmitter(Vector2 position, Vector2 direction)
    {
        Position = position;
        Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }

    public void Emit(MapModifierContext context, AttackEntityData attackEntityData)
    {
        float speed = attackEntityData.entityVelocity.magnitude;
        Vector2 velocity = Direction * speed;
        var buildContext = new AttackEntityBuildContext(
            attackEntityData,
            spellId: 0,
            spellInvocationId: 0,
            keyframeIndex: 0,
            modifications: null,
            attributeMask: SpellAttributeMask.None,
            position: new float2(Position.x, Position.y),
            velocity: new float2(velocity.x, velocity.y),
            direction: new float2(Direction.x, Direction.y));
        context.AttackEntityManager.Spawn(in buildContext);
    }
}
