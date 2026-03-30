/// <summary>
/// Per-frame behavior for one <see cref="SessionState"/>.
/// </summary>
public interface ISessionPhase
{
	void Enter(SessionFlowContext context);
	void Exit(SessionFlowContext context);
	void Tick(SessionFlowContext context, float deltaTime);
}
