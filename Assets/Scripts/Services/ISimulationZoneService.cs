using UnityEngine;

/// <summary>
/// Scene playfield rect and camera used for pointer mapping and combat presentation draw.
/// Registered by lab/game bootstrap via <see cref="EZServiceLocation.ServiceLocator"/>.
/// </summary>
public interface ISimulationZoneService
{
    RectTransform Zone { get; }

    /// <summary>
    /// Optional scene camera for UI raycasts; may be null when the canvas supplies one.
    /// </summary>
    Camera Camera { get; }
}
