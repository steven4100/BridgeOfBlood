/// <summary>
/// Applies a per-tick view snapshot for a session phase. Safe to call every frame while the phase is active.
/// </summary>
public interface IStatePresenter<in TViewData>
{
	void Render(TViewData data);
}
