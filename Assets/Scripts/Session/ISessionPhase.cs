/// <summary>
/// Per-frame behavior for one <see cref="SessionState"/>.
/// Enter is typed on <see cref="SessionPhaseBase{TContext}"/> and called only by <see cref="SessionFlowController"/>.
/// </summary>
public interface ISessionPhase
{
	SessionState State { get; }
	void Exit();
	void Tick(float deltaTime);
}
