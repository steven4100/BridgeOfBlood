using BridgeOfBlood.Data.Shared;

/// <summary>
/// Raised after all simulation steps have completed for a frame and before transient combat buffers are cleared.
/// Subscribers may read the native event arrays on <see cref="simulationState"/> during the callback only.
/// </summary>
public struct SimulationCompleteEvent : IEvent
{
    public GameSimulation.SimulationState simulationState;
    public float deltaTime;
    public float simulationTime;
    public bool simulationAdvanced;
    public SpellCastResult spellCastResult;
}
