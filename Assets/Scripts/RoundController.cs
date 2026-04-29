using System.Collections.Generic;
using System.Diagnostics;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using Unity.Mathematics;
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
/// Result of one frame of round simulation when the session layer must react.
/// </summary>
public struct RoundTickResult
{
	public bool roundEnded;
	public SessionState nextSessionState;
}

/// <summary>
/// Configuration passed to <see cref="RoundController"/> so it doesn't reference MonoBehaviour fields directly.
/// </summary>
public class RoundControllerConfig
{
	public KeyCode castInputKey;
	public bool debugLogTiming;
	/// <summary>Session clone from <see cref="GameConfig.CreateRuntimeCopy"/>; use <see cref="GameConfig.playerInventory"/> for items.</summary>
	public GameConfig gameConfig;
	public SpellModificationsTestData castModifications;
	public SimulationDebugController debugController;
}

/// <summary>
/// Session phase for <see cref="SessionState.Round"/> plus round simulation: player movement, casting, steps,
/// telemetry, damage/effect spawning, rendering, and phase evaluation. Uses <see cref="IRoundEndStrategy"/>
/// for win/lose and next session state when a round completes.
/// </summary>
public sealed class RoundController : SessionPhaseBase<RoundSessionViewData>
{
	readonly Player _player;
	readonly GameSimulation _simulation;
	readonly LoopedSpellCaster _loopedSpellCaster;
	readonly TelemetryAggregator _telemetryAggregator;
	readonly DamageNumberController _damageNumberController;
	readonly EffectSpriteController _effectSpriteController;
	readonly GameAudioManager _gameAudioManager;
	readonly SpriteInstanceBuilder _spriteInstanceBuilder;
	readonly SpriteInstancedRenderer _spriteRenderer;
	readonly AttackEntityDebugRenderer _attackDebugRenderer;
	readonly RoundControllerConfig _config;
	readonly EffectContext _effectContext = new EffectContext();
	readonly List<ItemEvalResult> _lastItemResults = new List<ItemEvalResult>();
	readonly IRoundEndStrategy _roundEndStrategy;
	readonly Camera _camera;

	public GameLoopPhase Phase { get; private set; }
	public int RoundNumber { get; private set; }
	public float BloodQuota { get; private set; }
	public int SpellLoopsPerRound { get; private set; }
	public float BloodExtractedThisRound { get; private set; }
	public bool QuotaMet { get; private set; }

	public List<ItemEvalResult> LastItemResults => _lastItemResults;
	

	public RoundController(
		Player player,
		GameSimulation simulation,
		LoopedSpellCaster loopedSpellCaster,
		TelemetryAggregator telemetryAggregator,
		DamageNumberController damageNumberController,
		EffectSpriteController effectSpriteController,
		GameAudioManager gameAudioManager,
		SpriteInstanceBuilder spriteInstanceBuilder,
		SpriteInstancedRenderer spriteRenderer,
		AttackEntityDebugRenderer attackDebugRenderer,
		RoundControllerConfig config,
		IRoundEndStrategy roundEndStrategy,
		IStatePresenter<RoundSessionViewData> roundHudPresenter,
		Camera camera)
		: base(roundHudPresenter ?? RoundSessionNoOpPresenter.Instance)
	{
		_player = player;
		_simulation = simulation;
		_loopedSpellCaster = loopedSpellCaster;
		_telemetryAggregator = telemetryAggregator;
		_damageNumberController = damageNumberController;
		_effectSpriteController = effectSpriteController;
		_gameAudioManager = gameAudioManager;
		_spriteInstanceBuilder = spriteInstanceBuilder;
		_spriteRenderer = spriteRenderer;
		_attackDebugRenderer = attackDebugRenderer;
		_config = config;
		_roundEndStrategy = roundEndStrategy ?? new QuotaBasedRoundEndStrategy();
		_camera = camera;

		RoundNumber = 1;
		Phase = GameLoopPhase.Playing;
		ApplyRoundRuntimeFromConfig();
	}

	/// <summary>
	/// Swap the active runtime config (e.g. Lose → Retry builds a new <see cref="GameConfig.CreateRuntimeCopy"/>).
	/// </summary>
	public void SetGameConfig(GameConfig runtime)
	{
		_config.gameConfig = runtime;
	}

	protected override void OnEnter(SessionFlowContext context)
	{
		PrepareForRoundAfterShop();
		context.RuntimeGameConfig.playerInventory.SpellCollection.ClearRuntimeSpellTracking();
		ResetForNewRound(context.SimulationZone.rect);
	}

	protected override RoundSessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		RoundTickResult result = TickSimulation(
			deltaTime,
			context.SimulationZone.rect,
			_camera,
			context.SimulationZone);

		if (result.roundEnded)
			context.Flow.SetState(result.nextSessionState);

		return BuildRoundSessionViewData(context);
	}

	RoundSessionViewData BuildRoundSessionViewData(SessionFlowContext context)
	{
		var spellLabels = new List<string>();
		IReadOnlyList<RuntimeSpell> runtimeSpells = context.RuntimeGameConfig.playerInventory.SpellCollection.RuntimeSpells;
		for (int i = 0; i < runtimeSpells.Count; i++)
		{
			SpellAuthoringData def = runtimeSpells[i].Definition;
			string label = def != null && def.ShopItemDefinition != null
				&& !string.IsNullOrEmpty(def.ShopItemDefinition.DisplayName)
				? def.ShopItemDefinition.DisplayName
				: def.name;
			spellLabels.Add(label);
		}

		int loopsRemaining = Mathf.Max(0, SpellLoopsPerRound - _loopedSpellCaster.LoopCount);

		var itemRows = new List<RoundItemRowViewData>();
		IReadOnlyList<Item> passive = context.RuntimeGameConfig.playerInventory.GetPassiveItems();
		IReadOnlyList<ItemEvalResult> evals = _lastItemResults;
		int ei = 0;
		for (int i = 0; i < passive.Count; i++)
		{
			Item item = passive[i];
			if (item == null)
				continue;
			if (ei >= evals.Count)
				break;
			ItemEvalResult ev = evals[ei++];
			string displayName = item.ShopItemDefinition != null
				&& !string.IsNullOrEmpty(item.ShopItemDefinition.DisplayName)
				? item.ShopItemDefinition.DisplayName
				: item.name;
			itemRows.Add(new RoundItemRowViewData(displayName, ev.applied));
		}

		return new RoundSessionViewData(
			spellLabels,
			_loopedSpellCaster.IndexOfLastCast,
			BloodQuota,
			BloodExtractedThisRound,
			loopsRemaining,
			itemRows);
	}

	/// <summary>
	/// Runs one frame of the round. Returns session transition when the round ends this frame.
	/// </summary>
	RoundTickResult TickSimulation(float deltaTime, Rect rect, Camera cam, RectTransform simulationZone)
	{
		var debugCtrl = _config.debugController;
		bool hasController = debugCtrl != null;
		if (hasController)
			debugCtrl.ProcessInput();

		_player.Update(deltaTime, rect);

		bool loopsExhausted = _loopedSpellCaster.LoopCount >= SpellLoopsPerRound;
		bool allowCasting = Phase == GameLoopPhase.Playing && !loopsExhausted;
		bool castRequested = allowCasting && Input.GetKeyDown(_config.castInputKey);
		var mods = _config.castModifications != null
			? _config.castModifications.GetModifications()
			: new SpellModifications();

		EvaluateItems(mods);

		var sim = _simulation.State;
		SpellCastResult castResult = _loopedSpellCaster.AttemptToCastNextSpell(
			sim.SimulationTime, _player.Position, castRequested, mods);
		PlayCastAudio(castResult);
		_loopedSpellCaster.Update(sim.SimulationTime, new float2(-1f, 0f));

		bool advanceTime = !hasController || debugCtrl.ShouldAdvanceTime;
		if (advanceTime)
		{
			float dt = hasController ? debugCtrl.DeltaTime : deltaTime;
			_simulation.AdvanceTime(dt);
		}

		Stopwatch sw = _config.debugLogTiming ? new Stopwatch() : null;
		long totalMs = 0;

		for (int i = 0; i < _simulation.StepCount; i++)
		{
			if (!hasController || debugCtrl.ShouldRunPhase(i, _simulation.GetStepName(i)))
			{
				sw?.Restart();
				_simulation.ExecuteStep(i);
				if (sw != null)
				{
					long ms = sw.ElapsedMilliseconds;
					totalMs += ms;
					if (_config.debugLogTiming)
						Debug.Log($"[Timing] {_simulation.GetStepName(i)}: {ms}ms");
				}
			}
		}

		CombatReactionProcessor.ProcessAfterSimulationFrame(
			sim.KillEvents,
			sim.StatusAilmentAppliedEvents,
			sim.EnemyBuffers,
			_config.gameConfig.playerInventory,
			_simulation.AttackEntityManager);

		float frameDt = hasController ? debugCtrl.DeltaTime : deltaTime;
		_telemetryAggregator.ProcessFrame(sim, frameDt, castResult);

		BloodExtractedThisRound = _telemetryAggregator.CurrentRound.aggregate.bloodExtracted;

		_damageNumberController.SpawnFromDamageEvents(sim.DamageEvents, sim.EnemyBuffers);
		_damageNumberController.SpawnFromTickDamageEvents(sim.TickDamageEvents, sim.EnemyBuffers);
		_effectSpriteController.SpawnFromDamageEvents(sim.DamageEvents);
		_gameAudioManager.EnqueueFromCombatEvents(sim.DamageEvents, sim.KillEvents);
		_simulation.ClearFrameCombatEvents();

		if (advanceTime)
		{
			float effectDt = hasController ? debugCtrl.DeltaTime : deltaTime;
			_damageNumberController.Update(effectDt);
			_effectSpriteController.Update(effectDt);
		}

		_spriteInstanceBuilder.Build(sim.EnemyBuffers, sim.AttackEntities, _effectSpriteController.GetEntities());
		_spriteRenderer.Render(_spriteInstanceBuilder.Buffer, _spriteInstanceBuilder.Count, simulationZone, cam);
		_attackDebugRenderer.Render(sim.AttackEntities, simulationZone, cam);
		_damageNumberController.Render(simulationZone, cam);

		if (hasController)
			debugCtrl.NotifyFrameComplete();

		UpdatePhase(
			loopsExhausted,
			_loopedSpellCaster.HasActiveCasts,
			_loopedSpellCaster.HasPendingSpawns,
			sim.AttackEntityCount);

		if (Phase == GameLoopPhase.RoundEnd)
		{
			_telemetryAggregator.EndRound();
			RoundEndEvaluationInput endInput = new RoundEndEvaluationInput(
				BloodExtractedThisRound,
				BloodQuota,
				RoundNumber);
			RoundEndEvaluationResult resolution = _roundEndStrategy.Evaluate(in endInput);
			QuotaMet = resolution.QuotaMet;
			Phase = resolution.NextInternalPhase;
			return new RoundTickResult
			{
				roundEnded = true,
				nextSessionState = resolution.NextSessionState
			};
		}

		return default;
	}

	/// <summary>
	/// Clears simulation, spell caster, and repositions the player for a fresh round.
	/// Called from <see cref="Enter"/> after spell-loop sync.
	/// </summary>
	public void ResetForNewRound(Rect simulationRect)
	{
		_simulation.ResetForNewRound();
		_loopedSpellCaster.Reset();
		_loopedSpellCaster.ClearCastState();
		_player.PlaceAtRightSide(simulationRect);
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
		GameConfig gc = _config.gameConfig;
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

	void EvaluateItems(SpellModifications mods)
	{
		_effectContext.frameMetrics = _telemetryAggregator.CurrentFrame.aggregate;
		_effectContext.spellCastMetrics = _telemetryAggregator.CurrentSpellCast.aggregate;
		_effectContext.spellLoopMetrics = _telemetryAggregator.CurrentSpellLoop.aggregate;
		_effectContext.roundMetrics = _telemetryAggregator.CurrentRound.aggregate;
		_effectContext.gameMetrics = _telemetryAggregator.Game.aggregate;
		_effectContext.spellModifications = mods;

		_effectContext.spellInvocation = new SpellInvocationContext
		{
			totalSpellsCasted = _loopedSpellCaster.TotalInvocationCount,
			spellLoopNumber = _loopedSpellCaster.LoopCount + 1,
			spellSlotNumber = _loopedSpellCaster.NextCastIndex + 1,
			spellLoopSlotCount = _loopedSpellCaster.SpellCount,
			spellLoopsPerRound = SpellLoopsPerRound,
			spells = _loopedSpellCaster.Spells,
		};

		_lastItemResults.Clear();
		var items = _config.gameConfig.playerInventory.GetPassiveItems();
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			if (item == null) continue;
			_lastItemResults.Add(new ItemEvalResult
			{
				itemName = item.name,
				applied = item.Apply(_effectContext)
			});
		}
	}

	void PlayCastAudio(in SpellCastResult castResult)
	{
		if (!castResult.didCast)
			return;

		IReadOnlyList<RuntimeSpell> spells = _loopedSpellCaster.Spells;
		for (int i = 0; i < spells.Count; i++)
		{
			RuntimeSpell spell = spells[i];
			if (spell.spellId != castResult.spellId)
				continue;

			AudioUnit unit = spell.Definition.castAudio;
			if (unit != null)
			{
				uint seed = AttackEntityBuildRngSeed.Mix(
					castResult.spellId,
					castResult.invocationCount,
					0,
					0x41A3F5C);
				_gameAudioManager.RequestOneShot(unit.ToRuntime(seed), _player.Position);
			}
			return;
		}
	}
}
