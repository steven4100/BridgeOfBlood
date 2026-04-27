/// <summary>
/// Use when no <see cref="RoundPanelPresenter"/> is wired; round phase logic still runs.
/// </summary>
public sealed class RoundSessionNoOpPresenter : IStatePresenter<RoundSessionViewData>
{
	public static readonly RoundSessionNoOpPresenter Instance = new RoundSessionNoOpPresenter();

	RoundSessionNoOpPresenter() { }

	public void Render(RoundSessionViewData data) { }

	public void SetRootVisible(bool visible) { }
}
