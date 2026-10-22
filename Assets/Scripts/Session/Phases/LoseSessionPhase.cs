using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class LoseSessionPhase : SessionPhaseBase<LoseSessionPhase.Context>
{
	public readonly struct Context
	{
	}

	readonly Func<GameConfig> _createRuntimeGameConfigCopy;

	public override SessionState State => SessionState.Lose;

	public LoseSessionPhase(SessionFlowController controller, Func<GameConfig> createRuntimeGameConfigCopy)
		: base(controller)
	{
		_createRuntimeGameConfigCopy = createRuntimeGameConfigCopy;
	}

	protected override void OnTick(Context context, float deltaTime)
	{
		if (!Input.GetKeyDown(KeyCode.R))
			controller.EnterRound(new RoundController.Context(_createRuntimeGameConfigCopy()));
	}

	protected override void OnEnter(Context context) { }

	protected override void OnExit(Context context) { }
}
