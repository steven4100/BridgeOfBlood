using System.Collections.Generic;

/// <summary>
/// One passive item row for the round HUD (Active = effect applied this frame).
/// </summary>
public readonly struct RoundItemRowViewData
{
	public readonly string DisplayName;
	public readonly bool IsActive;

	public RoundItemRowViewData(string displayName, bool isActive)
	{
		DisplayName = displayName;
		IsActive = isActive;
	}
}

/// <summary>
/// Round HUD snapshot for one session tick.
/// </summary>
public readonly struct RoundSessionViewData
{
	public readonly List<string> SpellSlotLabels;
	public readonly int IndexOfLastCastInLoop;
	public readonly float BloodQuota;
	public readonly float BloodExtracted;
	public readonly int LoopsRemaining;
	public readonly List<RoundItemRowViewData> ItemRows;

	public RoundSessionViewData(
		List<string> spellSlotLabels,
		int indexOfLastCastInLoop,
		float bloodQuota,
		float bloodExtracted,
		int loopsRemaining,
		List<RoundItemRowViewData> itemRows)
	{
		SpellSlotLabels = spellSlotLabels;
		IndexOfLastCastInLoop = indexOfLastCastInLoop;
		BloodQuota = bloodQuota;
		BloodExtracted = bloodExtracted;
		LoopsRemaining = loopsRemaining;
		ItemRows = itemRows;
	}
}
