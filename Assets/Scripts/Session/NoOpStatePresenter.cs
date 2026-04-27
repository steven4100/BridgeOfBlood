/// <summary>
/// Discards <see cref="EmptySessionViewData"/>; use for phases without a presenter.
/// </summary>
public sealed class NoOpStatePresenter : IStatePresenter<EmptySessionViewData>
{
	public static readonly NoOpStatePresenter Instance = new NoOpStatePresenter();

	NoOpStatePresenter() { }

	public void Render(EmptySessionViewData data) { }

	public void SetRootVisible(bool visible) { }
}
