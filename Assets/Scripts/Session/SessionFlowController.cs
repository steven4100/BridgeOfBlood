using System;

/// <summary>
/// Owns <see cref="SessionState"/> and dispatches Enter / Exit / Tick to the active <see cref="ISessionPhase"/>.
/// Transitions are requested with the destination phase's context and applied on the next <see cref="Tick"/>.
/// </summary>
public sealed class SessionFlowController
{
	RoundController _round;
	ShopSessionPhase _shop;
	MapModSessionPhase _mapMod;

	ISessionPhase _active;
	Action _pending;

	public SessionState CurrentState { get; private set; } = SessionState.Shop;

	public void AddRound(RoundController phase) => _round = phase;

	public void AddShop(ShopSessionPhase phase) => _shop = phase;

	public void AddMapMod(MapModSessionPhase phase) => _mapMod = phase;

	public void EnterRound(RoundController.Context context)
	{
		_pending = () => Activate(_round, context);
	}

	public void EnterShop(ShopSessionPhase.Context context)
	{
		_pending = () => Activate(_shop, context);
	}

	public void EnterMapMod(MapModSessionPhase.Context context)
	{
		_pending = () => Activate(_mapMod, context);
	}

	public void Tick(float deltaTime)
	{
		ApplyPending();
		_active.Tick(deltaTime);
	}

	/// <summary>
	/// Run <see cref="ISessionPhase.Exit"/> for the active phase (e.g. scene teardown so shop events unsubscribe).
	/// </summary>
	public void Shutdown()
	{
		if (_active != null)
			_active.Exit();
	}

	void ApplyPending()
	{
		Action pending = _pending;
		if (pending == null)
			return;

		_pending = null;
		pending();
	}

	void Activate<TContext>(SessionPhaseBase<TContext> phase, TContext context)
	{
		if (_active == phase)
			return;

		if (_active != null)
			_active.Exit();

		phase.Enter(context);
		_active = phase;
		CurrentState = phase.State;
	}
}
