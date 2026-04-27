using UnityEngine;

/// <summary>
/// Input for <see cref="IRoundEndStrategy.Evaluate"/> after a round's simulation has fully wound down.
/// </summary>
public readonly struct RoundEndEvaluationInput
{
	public readonly float BloodExtractedThisRound;
	public readonly float BloodQuota;
	public readonly int RoundNumber;

	public RoundEndEvaluationInput(float bloodExtractedThisRound, float bloodQuota, int roundNumber)
	{
		BloodExtractedThisRound = bloodExtractedThisRound;
		BloodQuota = bloodQuota;
		RoundNumber = roundNumber;
	}
}

/// <summary>
/// Outcome of round-end policy: quota flag, next session state, and internal <see cref="GameLoopPhase"/>.
/// </summary>
public readonly struct RoundEndEvaluationResult
{
	public readonly bool QuotaMet;
	public readonly SessionState NextSessionState;
	public readonly GameLoopPhase NextInternalPhase;

	public RoundEndEvaluationResult(bool quotaMet, SessionState nextSessionState, GameLoopPhase nextInternalPhase)
	{
		QuotaMet = quotaMet;
		NextSessionState = nextSessionState;
		NextInternalPhase = nextInternalPhase;
	}
}

/// <summary>
/// Encapsulates win/lose determination and where the session goes after a round completes.
/// </summary>
public interface IRoundEndStrategy
{
	RoundEndEvaluationResult Evaluate(in RoundEndEvaluationInput input);
}

/// <summary>
/// Default rule: meet blood quota → shop; otherwise → lose. Matches legacy <c>BloodExtracted &gt;= BloodQuota</c> behavior.
/// </summary>
public sealed class QuotaBasedRoundEndStrategy : IRoundEndStrategy
{
	public RoundEndEvaluationResult Evaluate(in RoundEndEvaluationInput input)
	{
		bool met = input.BloodExtractedThisRound >= input.BloodQuota;
		GameLoopPhase internalPhase = met ? GameLoopPhase.RoundEnd : GameLoopPhase.Lose;
		SessionState session = met ? SessionState.Shop : SessionState.Lose;
		Debug.Log(
			$"[RoundController] Round {input.RoundNumber} ended. Blood: {input.BloodExtractedThisRound:F0} / {input.BloodQuota:F0} — {(met ? "QUOTA MET" : "QUOTA FAILED")}");
		return new RoundEndEvaluationResult(met, session, internalPhase);
	}
}
