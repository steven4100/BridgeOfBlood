/// <summary>
/// Applies a per-tick view snapshot for a session phase. Safe to call every frame while the phase is active.
/// <see cref="SessionPhaseBase{TViewData}"/> also calls <see cref="SetRootVisible"/> after <see cref="SessionPhaseBase{TViewData}.OnEnter"/>
/// and before <see cref="SessionPhaseBase{TViewData}.OnExit"/>; use a no-op when the presenter has no single root to toggle.
/// </summary>
public interface IStatePresenter<in TViewData>
{
	void Render(TViewData data);

	void SetRootVisible(bool visible);
}
