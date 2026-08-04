using UnityEngine;

/// <summary>
/// Plain holder for <see cref="ISimulationZoneService"/> registered at session bootstrap.
/// </summary>
public sealed class SimulationZoneService : ISimulationZoneService
{
    public RectTransform Zone { get; }
    public Camera Camera { get; }

    public SimulationZoneService(RectTransform zone, Camera camera)
    {
        Zone = zone;
        Camera = camera;
    }
}
