using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Shared dependencies for <see cref="ISessionPhase"/> implementations.
/// Pure data – no callbacks or delegates.
/// </summary>
public sealed class SessionFlowContext
{
	public SessionFlowController Flow { get; internal set; }
	public GameConfig RuntimeGameConfig { get; set; }
	public RoundController RoundController { get; }
	public ShopPanelPresenter ShopPanel { get; }
	public SpellCollection SpellCollection { get; }
	public RectTransform SimulationZone { get; }

	public SessionFlowContext(
		GameConfig runtimeGameConfig,
		RoundController roundController,
		ShopPanelPresenter shopPanel,
		SpellCollection spellCollection,
		RectTransform simulationZone)
	{
		RuntimeGameConfig = runtimeGameConfig;
		RoundController = roundController;
		ShopPanel = shopPanel;
		SpellCollection = spellCollection;
		SimulationZone = simulationZone;
	}
}
