using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyCombineMapModifier",
    menuName = "BridgeOfBlood/Map Modifiers/Enemy Combine")]
public sealed class EnemyCombineMapModifierDefinition : MapModifierDefinition
{
    [SerializeField, Min(0.5f)] float defaultRadius = 12f;

    public override MapModifierPlacementMode PlacementMode => MapModifierPlacementMode.Region;
    public override float DefaultRadius => defaultRadius;

    public override MapModifier CreateRuntime(in MapModifierPlacement placement)
    {
        return new EnemyCombineMapModifier(placement.Origin, placement.Radius);
    }
}
