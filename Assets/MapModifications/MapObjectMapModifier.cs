using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

/// <summary>Places one permanent map object. Reapplied after every simulation reset.</summary>
public sealed class MapObjectMapModifier : MapModifier
{
    readonly SpriteProvider _visual;
    readonly IReadOnlyList<MapObjectBehavior> _behaviors;

    public Vector2 Position { get; }
    public float Radius { get; }

    public MapObjectMapModifier(
        Vector2 position,
        float radius,
        SpriteProvider visual,
        IReadOnlyList<MapObjectBehavior> behaviors)
    {
        Position = position;
        Radius = radius;
        _visual = visual;
        _behaviors = behaviors;
    }

    public override IEnumerable<CombatSimWait> Apply(MapModifierContext context)
    {
        var position = new float2(Position.x, Position.y);
        EntityVisual visual = _visual != null ? _visual.Resolve(1u) : EntityVisual.None;
        EntityId id = context.EnemyManager.CreateMapObject(position, Radius, visual);
        context.MapObjects.Register(id, _behaviors);
        context.EnemyManager.BuildGrid();
        yield break;
    }
}

[CreateAssetMenu(fileName = "MapObjectMapModifier", menuName = "BridgeOfBlood/Map Modifiers/Map Object")]
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
