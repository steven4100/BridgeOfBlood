using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class LoseSessionPhase : SessionPhaseBase<EmptySessionViewData>
{
	readonly Func<GameConfig> _createRuntimeGameConfigCopy;

	public LoseSessionPhase(
		IStatePresenter<EmptySessionViewData> presenter,
		Func<GameConfig> createRuntimeGameConfigCopy)
		: base(presenter)
	{
		_createRuntimeGameConfigCopy = createRuntimeGameConfigCopy;
	}

	protected override EmptySessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		if (!Input.GetKeyDown(KeyCode.R))
			return default;

		context.Flow.SetState(SessionState.Round);
		context.RuntimeGameConfig = _createRuntimeGameConfigCopy();
		context.RoundController.SetGameConfig(context.RuntimeGameConfig);
		context.RoundController.Retry();
		return default;
	}
}
