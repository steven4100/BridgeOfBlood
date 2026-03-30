/// <summary>
/// High-level session phases (pregame, shop, etc.). Driven by <see cref="SessionFlowController"/>.
/// </summary>
public enum SessionState
{
	Pregame,
	Round,
	Shop,
	Lose
}
