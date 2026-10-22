using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class PregameSessionPhase : SessionPhaseBase<PregameSessionPhase.Context>
{
	public readonly struct Context
	{
	}

	readonly Func<GameConfig> _runtimeGameConfig;

	public override SessionState State => SessionState.Pregame;

	public PregameSessionPhase(SessionFlowController controller, Func<GameConfig> runtimeGameConfig)
		: base(controller)
	{
		_runtimeGameConfig = runtimeGameConfig;
	}

	protected override void OnTick(Context context, float deltaTime)
	{
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
			controller.EnterRound(new RoundController.Context(_runtimeGameConfig()));
	}

	protected override void OnEnter(Context context)
	{
	}

	protected override void OnExit(Context context)
	{
		throw new NotImplementedException();
	}
}
