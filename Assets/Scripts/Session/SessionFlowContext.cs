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
	public SpellCollection SpellCollection { get; }
	public RectTransform SimulationZone { get; }

	public SessionFlowContext(
		GameConfig runtimeGameConfig,
		RoundController roundController,
		SpellCollection spellCollection,
		RectTransform simulationZone)
	{
		RuntimeGameConfig = runtimeGameConfig;
		SpellCollection = spellCollection;
		SimulationZone = simulationZone;
	}
}
