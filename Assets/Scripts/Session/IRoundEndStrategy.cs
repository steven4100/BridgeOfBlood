using UnityEngine;

/// <summary>
/// Input for <see cref="IRoundEndStrategy.Evaluate"/> after a round's simulation has fully wound down.
/// </summary>
public readonly struct RoundEndEvaluationInput
{
	public readonly int KillsThisRound;
	public readonly int MinEnemiesKilled;
	public readonly int EnemiesSpawnedThisRound;
	public readonly float KillQuotaPercent;
	public readonly int RoundNumber;

	public RoundEndEvaluationInput(
		int killsThisRound,
		int minEnemiesKilled,
		int enemiesSpawnedThisRound,
		float killQuotaPercent,
		int roundNumber)
	{
		KillsThisRound = killsThisRound;
		MinEnemiesKilled = minEnemiesKilled;
		EnemiesSpawnedThisRound = enemiesSpawnedThisRound;
		KillQuotaPercent = killQuotaPercent;
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
/// Default rule: meet kill quota → shop; otherwise → lose.
/// </summary>
public sealed class QuotaBasedRoundEndStrategy : IRoundEndStrategy
{
	public RoundEndEvaluationResult Evaluate(in RoundEndEvaluationInput input)
	{
		bool met = input.KillsThisRound >= input.MinEnemiesKilled;
		GameLoopPhase internalPhase = met ? GameLoopPhase.RoundEnd : GameLoopPhase.Lose;
		SessionState session = met ? SessionState.Shop : SessionState.Lose;
		Debug.Log(
			$"[RoundController] Round {input.RoundNumber} ended. Kills: {input.KillsThisRound} / {input.MinEnemiesKilled} ({input.KillQuotaPercent:0.#}% of {input.EnemiesSpawnedThisRound} spawned) — {(met ? "QUOTA MET" : "QUOTA FAILED")}");
		return new RoundEndEvaluationResult(met, session, internalPhase);
	}
}
