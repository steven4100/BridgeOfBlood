using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

/// <summary>
/// Shared dependencies for <see cref="ISessionPhase"/> implementations.
/// </summary>
public sealed class SessionFlowContext
{
	public SessionFlowController Flow { get; internal set; }
	/// <summary>Always resolves the current session clone (e.g. after <see cref="CreateRuntimeGameConfigCopy"/> on retry).</summary>
	public GameConfig RuntimeGameConfig => _getRuntimeGameConfig();
	readonly Func<GameConfig> _getRuntimeGameConfig;
	public RoundController RoundController { get; }
	public ShopPanelPresenter ShopPanel { get; }
	public ShopController ShopController { get; }
	public SpellCollection SpellCollection { get; }
	public Action ResetForNewRound { get; }
	public Action CreateRuntimeGameConfigCopy { get; }
	public Func<Rect> GetSimulationRect { get; }
	public RectTransform SimulationZone { get; }
	public Func<Camera> GetCamera { get; }

	public SessionFlowContext(
		Func<GameConfig> getRuntimeGameConfig,
		RoundController roundController,
		ShopPanelPresenter shopPanel,
		ShopController shopController,
		SpellCollection spellCollection,
		Action resetForNewRound,
		Action createRuntimeGameConfigCopy,
		Func<Rect> getSimulationRect,
		RectTransform simulationZone,
		Func<Camera> getCamera)
	{
		_getRuntimeGameConfig = getRuntimeGameConfig;
		RoundController = roundController;
		ShopPanel = shopPanel;
		ShopController = shopController;
		SpellCollection = spellCollection;
		ResetForNewRound = resetForNewRound;
		CreateRuntimeGameConfigCopy = createRuntimeGameConfigCopy;
		GetSimulationRect = getSimulationRect;
		SimulationZone = simulationZone;
		GetCamera = getCamera;
	}
}
