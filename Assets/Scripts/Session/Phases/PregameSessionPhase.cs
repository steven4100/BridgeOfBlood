using UnityEngine;

public sealed class PregameSessionPhase : SessionPhaseBase<EmptySessionViewData>
{
	public PregameSessionPhase(IStatePresenter<EmptySessionViewData> presenter) : base(presenter) { }

	protected override EmptySessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
		{
			context.Flow.SetState(SessionState.Round);
			context.ResetForNewRound();
		}
		return default;
	}
}
