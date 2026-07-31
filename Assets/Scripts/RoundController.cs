using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Phases within a single round. Managed by <see cref="RoundController"/>.
/// </summary>
public enum GameLoopPhase
{
	Playing,
	AwaitingDespawn,
	RoundEnd,
	Lose
}

/// <summary>
/// Round-session knobs for <see cref="RoundController"/>. Combat frame wiring lives on
/// <see cref="CombatSimulationControllerConfig"/>.
/// </summary>
public class RoundControllerConfig
{
	public KeyCode castInputKey;
}

/// <summary>
/// Session phase for <see cref="SessionState.Round"/>: cast gating, round phase/quota evaluation.
/// Combat frame coordination (including item evaluation) is delegated to
/// <see cref="CombatSimulationController"/>. Uses <see cref="IRoundEndStrategy"/> for win/lose
/// when a round completes.
/// </summary>
public sealed class RoundController : SessionPhaseBase
{
	readonly CombatSimulationController _combatSimulation;
	readonly RoundControllerConfig _config;
	readonly IRoundEndStrategy _roundEndStrategy;

	public GameLoopPhase Phase { get; private set; }
	public int RoundNumber { get; private set; }
	public float BloodQuota { get; private set; }
	public int SpellLoopsPerRound { get; private set; }
	public float BloodExtractedThisRound { get; private set; }
	public bool QuotaMet { get; private set; }

	public IReadOnlyList<ItemEvalResult> LastItemResults => _combatSimulation.LastItemResults;

	public RoundController(
		CombatSimulationController combatSimulation,
		RoundControllerConfig config,
		IRoundEndStrategy roundEndStrategy,
		SessionFlowController sessionFlowController)
		: base(sessionFlowController)
	{
		_combatSimulation = combatSimulation;
		_config = config;
		_roundEndStrategy = roundEndStrategy ?? new QuotaBasedRoundEndStrategy();

		RoundNumber = 1;
		Phase = GameLoopPhase.Playing;
		ApplyRoundRuntimeFromConfig();
	}

	/// <summary>
	/// Swap the active runtime config (e.g. Lose → Retry builds a new <see cref="GameConfig.CreateRuntimeCopy"/>).
	/// </summary>
	public void SetGameConfig(GameConfig runtime)
	{
		_combatSimulation.SetRuntimeGameConfig(runtime);
	}

	protected override void OnEnter(SessionFlowContext context)
	{
		SetGameConfig(context.RuntimeGameConfig);
		PrepareForRoundAfterShop();
		context.RuntimeGameConfig.playerInventory.SpellCollection.ClearRuntimeSpellTracking();
		ResetForNewRound();
		SharedGameEventBus.Bus.Raise(new RoundEnterEvent
		{
			state = SessionState.Round,
			roundNumber = RoundNumber,
			bloodQuota = BloodQuota,
			spellLoopsPerRound = SpellLoopsPerRound,
			playfield = _combatSimulation.Simulation.State.Playfield
		});
	}

	protected override void OnExit(SessionFlowContext context)
	{
		SharedGameEventBus.Bus.Raise(new RoundExitEvent
		{
			state = SessionState.Round,
			roundNumber = RoundNumber,
			bloodExtracted = BloodExtractedThisRound,
			quotaMet = QuotaMet
		});
	}

	public override void Tick(SessionFlowContext context, float deltaTime)
	{
		LoopedSpellCaster caster = _combatSimulation.LoopedSpellCaster;
		bool loopsExhausted = caster.LoopCount >= SpellLoopsPerRound;
		bool allowCasting = Phase == GameLoopPhase.Playing && !loopsExhausted;
		bool castRequested = allowCasting && Input.GetKeyDown(_config.castInputKey);

		_combatSimulation.Tick(deltaTime, castRequested, SpellLoopsPerRound);

		BloodExtractedThisRound = _combatSimulation.TelemetryAggregator.CurrentRound.aggregate.bloodExtracted;

		var sim = _combatSimulation.Simulation.State;
		UpdatePhase(
			loopsExhausted,
			caster.HasActiveCasts,
			caster.HasPendingSpawns,
			sim.AttackEntityCount);

		if (Phase == GameLoopPhase.RoundEnd)
		{
			_combatSimulation.TelemetryAggregator.EndRound();
			RoundEndEvaluationInput endInput = new RoundEndEvaluationInput(
				BloodExtractedThisRound,
				BloodQuota,
				RoundNumber);
			RoundEndEvaluationResult resolution = _roundEndStrategy.Evaluate(in endInput);
			QuotaMet = resolution.QuotaMet;
			Phase = resolution.NextInternalPhase;
		}
	}

	/// <summary>
	/// Clears simulation, spell caster, and repositions the player for a fresh round.
	/// Called from <see cref="Enter"/> after spell-loop sync.
	/// </summary>
	public void ResetForNewRound()
	{
		_combatSimulation.ResetForNewRound();
	}

	/// <summary>
	/// If the last round ended with quota met, advances round index and reapplies runtime tuning;
	/// otherwise no-op on round index (e.g. opening shop before round 1).
	/// </summary>
	public void PrepareForRoundAfterShop()
	{
		if (Phase == GameLoopPhase.RoundEnd)
			AdvanceToNextRoundAfterWin();
	}

	void AdvanceToNextRoundAfterWin()
	{
		RoundNumber++;
		BloodExtractedThisRound = 0f;
		QuotaMet = false;
		Phase = GameLoopPhase.Playing;
		ApplyRoundRuntimeFromConfig();
		Debug.Log($"[RoundController] Starting round {RoundNumber}. Quota: {BloodQuota:F0}, Loops: {SpellLoopsPerRound}");
	}

	/// <summary>
	/// Resets to round 1. Call from Lose → Round transition.
	/// </summary>
	public void Retry()
	{
		RoundNumber = 1;
		BloodExtractedThisRound = 0f;
		QuotaMet = false;
		Phase = GameLoopPhase.Playing;
		ApplyRoundRuntimeFromConfig();
		Debug.Log($"[RoundController] Retrying from round 1. Quota: {BloodQuota:F0}, Loops: {SpellLoopsPerRound}");
	}

	void ApplyRoundRuntimeFromConfig()
	{
		GameConfig gc = _combatSimulation.RuntimeGameConfig;
		BloodQuota = gc.bloodQuotaScaling.BuildForRound(RoundNumber).bloodRequirement;
		SpellLoopsPerRound = Mathf.Max(0, gc.maxSpellLoopsPerRound);
	}

	bool UpdatePhase(bool loopsExhausted, bool hasActiveCasts, bool hasPendingSpawns, int attackEntityCount)
	{
		GameLoopPhase before = Phase;

		switch (Phase)
		{
			case GameLoopPhase.Playing:
				if (loopsExhausted)
					Phase = GameLoopPhase.AwaitingDespawn;
				break;

			case GameLoopPhase.AwaitingDespawn:
				if (!hasActiveCasts && !hasPendingSpawns && attackEntityCount == 0)
					Phase = GameLoopPhase.RoundEnd;
				break;
		}

		return Phase != before;
	}
}
