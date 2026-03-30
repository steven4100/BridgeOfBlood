using System.Collections.Generic;

/// <summary>
/// Owns <see cref="SessionState"/> and dispatches Enter / Exit / Tick to the active <see cref="ISessionPhase"/>.
/// Transitions are applied by phases via <see cref="SetState"/>.
/// </summary>
public sealed class SessionFlowController
{
	readonly SessionFlowContext _context;
	readonly Dictionary<SessionState, ISessionPhase> _phases = new Dictionary<SessionState, ISessionPhase>();
	SessionState _currentState;
	SessionState? _lastHandledState;

	public SessionState CurrentState => _currentState;

	public SessionFlowController(SessionFlowContext context, PregameSessionPhase pregame, RoundSessionPhase round, ShopSessionPhase shop, LoseSessionPhase lose)
	{
		_context = context;
		context.Flow = this;
		_currentState = SessionState.Pregame;
		_phases[SessionState.Pregame] = pregame;
		_phases[SessionState.Round] = round;
		_phases[SessionState.Shop] = shop;
		_phases[SessionState.Lose] = lose;
	}

	public void SetState(SessionState next)
	{
		_currentState = next;
	}

	public void Tick(float deltaTime)
	{
		if (_lastHandledState == null)
		{
			_phases[_currentState].Enter(_context);
			_lastHandledState = _currentState;
		}
		else if (_currentState != _lastHandledState.Value)
		{
			_phases[_lastHandledState.Value].Exit(_context);
			_phases[_currentState].Enter(_context);
			_lastHandledState = _currentState;
		}

		// Tick only the phase that was active after transition sync; SetState during Tick applies next frame.
		SessionState phaseToTick = _lastHandledState.Value;
		_phases[phaseToTick].Tick(_context, deltaTime);
	}

	/// <summary>
	/// Run <see cref="ISessionPhase.Exit"/> for the current phase (e.g. scene teardown so shop events unsubscribe).
	/// </summary>
	public void Shutdown()
	{
		if (_lastHandledState != null)
			_phases[_lastHandledState.Value].Exit(_context);
	}
}
