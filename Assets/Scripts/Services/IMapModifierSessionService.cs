using System.Collections.Generic;

/// <summary>
/// Session-owned placed map modifiers.
/// Rebuilt with the runtime <see cref="GameConfig"/> so a new run starts empty.
/// </summary>
public interface IMapModifierSessionService
{
    IReadOnlyList<MapModifier> PlacedModifiers { get; }

    void AddPlaced(MapModifier modifier);
}
