using BridgeOfBlood.Data.Shared;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapObjectMapModifierDefinition", menuName = "BridgeOfBlood/Map Modifiers/Map Object")]
public sealed class MapObjectMapModifierDefinition : MapModifierDefinition
{
    [SerializeField, Min(0.5f)] float radius = 30f;
    [SerializeField] SpriteProvider visual;

    [SerializeReference]
    [SerializeInterface]
    List<MapObjectBehavior> behaviors = new List<MapObjectBehavior>();

    public float Radius => radius;

    public override MapModifierPlacementMode PlacementMode => MapModifierPlacementMode.Point;
    public override float DefaultRadius => radius;

    public override MapModifier CreateRuntime(in MapModifierPlacement placement)
    {
        return new MapObjectMapModifier(placement.Origin, radius, visual, behaviors);
    }
}
