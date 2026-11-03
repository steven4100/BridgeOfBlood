using UnityEngine;

/// <summary>
/// Session map: dimensions, the camera used for pointer rays, and the playfield plane
/// published by <see cref="SimulationRectRenderer"/>. Registered via <see cref="EZServiceLocation.ServiceLocator"/>.
/// </summary>
public interface IMapService
{
    Map Map { get; }

    /// <summary>
    /// Scene camera for pointer rays. May be null when the session did not assign one.
    /// </summary>
    Camera Camera { get; }

    /// <summary>
    /// Playfield-local to world. Local XY matches simulation space (x from the left edge, y from vertical center).
    /// </summary>
    Matrix4x4 LocalToWorld { get; }

    /// <summary>
    /// World-space facing of the playfield plane (<see cref="Transform.forward"/> of the visual rect).
    /// </summary>
    Vector3 PlayfieldForward { get; }

    bool HasPlayfieldTransform { get; }

    /// <summary>
    /// World bounds of the playfield rectangle, expanded for instanced draws.
    /// </summary>
    Bounds WorldBounds { get; }

    void SetPlayfieldTransform(Matrix4x4 localToWorld, Vector3 forward);

    bool TryScreenPositionToMapPosition(Map.MapCoordinateSystem mapCoordinateSystem, out Vector2 pos);
}
