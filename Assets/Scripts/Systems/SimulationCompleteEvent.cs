using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

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
    /// <summary>Player position in simulation-local space for presentation sync.</summary>
    public float2 playerPosition;
    /// <summary>Resolved spell modifications for this frame. Valid only during the event callback.</summary>
    public SpellModificationCollection frameModifications;
}
