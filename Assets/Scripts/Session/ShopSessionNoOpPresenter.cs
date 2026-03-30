/// <summary>
/// Use when no <see cref="ShopPanelPresenter"/> is wired (e.g. headless test); shop phase logic still runs.
/// </summary>
public sealed class ShopSessionNoOpPresenter : IStatePresenter<ShopSessionViewData>
{
	public static readonly ShopSessionNoOpPresenter Instance = new ShopSessionNoOpPresenter();

	ShopSessionNoOpPresenter() { }

	public void Render(ShopSessionViewData data) { }
}
