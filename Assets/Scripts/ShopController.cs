using UnityEngine;

/// <summary>
/// Result of one frame of the shop phase.
/// </summary>
public struct ShopTickResult
{
	public bool requestedNextRound;
}

/// <summary>
/// Shop phase placeholder: advance to the next round on N. Offerings and purchases come in a later iteration.
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
