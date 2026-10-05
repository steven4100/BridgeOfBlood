using UnityEngine;

public enum MapModifierPlacementMode
{
    Point,
    Directed,
    Region
}

public readonly struct MapModifierPlacement
{
    public readonly Vector2 Origin;
    public readonly Vector2 Direction;
    public readonly float Radius;

    public MapModifierPlacement(Vector2 origin, Vector2 direction, float radius)
    {
        Origin = origin;
        Direction = direction;
        Radius = radius;
    }
}

/// <summary>Designer-facing authoring data that creates one placed runtime map modifier.</summary>
public abstract class MapModifierDefinition : ScriptableObject
{
    [SerializeField] string displayName;

    public string DisplayName => displayName;
    public abstract MapModifierPlacementMode PlacementMode { get; }
    public abstract float DefaultRadius { get; }
    public abstract MapModifier CreateRuntime(in MapModifierPlacement placement);
}
