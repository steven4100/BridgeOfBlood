/// <summary>
/// Runs phase logic via <see cref="TickAndBuildViewData"/>, then passes the result to the presenter every tick.
/// Calls <see cref="IStatePresenter{TViewData}.SetRootVisible"/> after <see cref="OnEnter"/> and before <see cref="OnExit"/>.
/// </summary>
public abstract class SessionPhaseBase<TViewData> : ISessionPhase
{
	readonly IStatePresenter<TViewData> _presenter;

	protected SessionPhaseBase(IStatePresenter<TViewData> presenter)
	{
		_presenter = presenter;
	}

	public void Enter(SessionFlowContext context)
	{
		OnEnter(context);
		_presenter.SetRootVisible(true);
	}

	public void Exit(SessionFlowContext context)
	{
		_presenter.SetRootVisible(false);
		OnExit(context);
	}

	/// <summary>Phase-specific enter logic; <see cref="IStatePresenter{TViewData}.SetRootVisible"/> runs after this returns.</summary>
	protected virtual void OnEnter(SessionFlowContext context) { }

	/// <summary>Phase-specific exit logic; presenter root is hidden before this runs.</summary>
	protected virtual void OnExit(SessionFlowContext context) { }

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
