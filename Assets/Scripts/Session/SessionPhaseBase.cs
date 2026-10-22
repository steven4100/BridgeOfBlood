/// <summary>
/// Shared Enter / Exit / Tick wiring for one <see cref="SessionState"/>.
/// The context supplied to <see cref="Enter"/> is stored and passed to exit and tick.
/// </summary>
public abstract class SessionPhaseBase<TContext> : ISessionPhase
{
	protected readonly SessionFlowController controller;
	TContext _context;

	protected SessionPhaseBase(SessionFlowController controller)
	{
		this.controller = controller;
	}

	public abstract SessionState State { get; }

	internal void Enter(TContext context)
	{
		_context = context;
		OnEnter(context);
	}

	public void Exit() => OnExit(_context);

	public void Tick(float deltaTime) => OnTick(_context, deltaTime);

	protected abstract void OnEnter(TContext context);

	protected abstract void OnExit(TContext context);

	protected abstract void OnTick(TContext context, float deltaTime);
}
