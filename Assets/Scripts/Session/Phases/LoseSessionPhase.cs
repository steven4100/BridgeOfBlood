using UnityEngine;

public sealed class LoseSessionPhase : SessionPhaseBase<EmptySessionViewData>
{
	public LoseSessionPhase(IStatePresenter<EmptySessionViewData> presenter) : base(presenter) { }

	protected override EmptySessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		if (!Input.GetKeyDown(KeyCode.R))
			return default;

		context.Flow.SetState(SessionState.Round);
		context.CreateRuntimeGameConfigCopy();
		context.RoundController.SetGameConfig(context.RuntimeGameConfig);
		context.RoundController.Retry();
		context.ResetForNewRound();
		return default;
	}
}
