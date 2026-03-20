using UnityEngine;

/// <summary>
/// Result of one frame of the shop phase.
/// </summary>
public struct ShopTickResult
{
    public bool requestedNextRound;
}

/// <summary>
/// Handles the shop phase: reads input, manages item purchasing (future),
/// and signals when the player is ready to proceed to the next round.
/// </summary>
public class ShopController
{
    public ShopTickResult Tick()
    {
        if (Input.GetKeyDown(KeyCode.N))
            return new ShopTickResult { requestedNextRound = true };

        return default;
    }
}
