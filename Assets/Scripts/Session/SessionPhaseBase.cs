/// <summary>
/// Shared Enter / Exit / Tick wiring for one <see cref="SessionState"/>.
/// </summary>
public abstract class SessionPhaseBase : ISessionPhase
{
	protected SessionFlowController controller;
	protected SessionPhaseBase(SessionFlowController controller)
	{
		this.controller = controller;
	}

	public void Enter(SessionFlowContext context)
	{
		OnEnter(context);
	}

	public void Exit(SessionFlowContext context)
	{
		OnExit(context);
	}

	protected abstract void OnEnter(SessionFlowContext context);

	protected abstract void OnExit(SessionFlowContext context);

	public abstract void Tick(SessionFlowContext context, float deltaTime);
}
