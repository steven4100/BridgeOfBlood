/// <summary>
/// Runs phase logic via <see cref="TickAndBuildViewData"/>, then passes the result to the presenter every tick.
/// </summary>
public abstract class SessionPhaseBase<TViewData> : ISessionPhase
{
	readonly IStatePresenter<TViewData> _presenter;

	protected SessionPhaseBase(IStatePresenter<TViewData> presenter)
	{
		_presenter = presenter;
	}

	public virtual void Enter(SessionFlowContext context) { }

	public virtual void Exit(SessionFlowContext context) { }

	public void Tick(SessionFlowContext context, float deltaTime)
	{
		TViewData data = TickAndBuildViewData(context, deltaTime);
		_presenter.Render(data);
	}

	/// <summary>
	/// Run this phase's simulation and session logic for the tick, then return the view snapshot to render.
	/// </summary>
	protected abstract TViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime);
}
