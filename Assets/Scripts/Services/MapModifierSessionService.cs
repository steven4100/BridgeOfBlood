using System.Collections.Generic;

/// <summary>In-memory map-modifier store for one session run.</summary>
public sealed class MapModifierSessionService : IMapModifierSessionService
{
    readonly List<MapModifier> _placedModifiers = new List<MapModifier>();

    public IReadOnlyList<MapModifier> PlacedModifiers => _placedModifiers;

    public void AddPlaced(MapModifier modifier)
    {
        _placedModifiers.Add(modifier);
    }
}
