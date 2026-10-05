using UnityEngine;

public enum MapModifierSettleStep
{
    OneTick,
    AttackEntitiesDespawned,
    KnockbackSettled
}

[CreateAssetMenu(
    fileName = "AttackEntityMapModifier",
    menuName = "BridgeOfBlood/Map Modifiers/Attack Entity Spawn")]
public sealed class AttackEntitySpawnMapModifierDefinition : MapModifierDefinition
{
    [SerializeField] AttackEntityData attackEntityData;
    [SerializeField] bool requiresDirection;
    [SerializeField] MapModifierSettleStep[] settleSteps;

    public AttackEntityData AttackEntityData => attackEntityData;
    public override MapModifierPlacementMode PlacementMode =>
        requiresDirection
            ? MapModifierPlacementMode.Directed
            : MapModifierPlacementMode.Point;
    public override float DefaultRadius => 0f;

    public override MapModifier CreateRuntime(in MapModifierPlacement placement)
    {
        IAttackEntityEmitter emitter = requiresDirection
            ? new DirectedAttackEntityEmitter(placement.Origin, placement.Direction)
            : new PointAttackEntityEmitter(placement.Origin);
        return new AttackEntitySpawnMapModifier(attackEntityData, emitter, settleSteps);
    }
}
