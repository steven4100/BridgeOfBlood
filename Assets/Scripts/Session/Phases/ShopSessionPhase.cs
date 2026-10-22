using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class ShopSessionPhase : SessionPhaseBase<ShopSessionPhase.Context>
{
	public readonly struct Context
	{
	}

	readonly Func<GameConfig> _runtimeGameConfig;

	public override SessionState State => SessionState.Shop;

	public ShopSessionPhase(SessionFlowController controller, Func<GameConfig> runtimeGameConfig)
		: base(controller)
	{
		_runtimeGameConfig = runtimeGameConfig;
	}

	protected override void OnEnter(Context context)
	{
		SharedGameEventBus.Bus.SubscribeTo<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.SubscribeTo<MapModPlacementRequested>(OnMapModPlacementRequested);
		SharedGameEventBus.Bus.Raise(new ShopEnterEvent());
	}

	protected override void OnExit(Context context)
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.UnsubscribeFrom<MapModPlacementRequested>(OnMapModPlacementRequested);
		SharedGameEventBus.Bus.Raise(new ShopExitEvent());
	}

	void OnShopContinueButtonPressed(ref ShopContinueButtonPressed @event)
	{
		controller.EnterRound(new RoundController.Context(_runtimeGameConfig()));
	}

	void OnMapModPlacementRequested(ref MapModPlacementRequested @event)
	{
		controller.EnterMapMod(new MapModSessionPhase.Context(@event.placementCount));
	}

	protected override void OnTick(Context context, float deltaTime)
	{
		if (Input.GetKeyDown(KeyCode.N))
			controller.EnterRound(new RoundController.Context(_runtimeGameConfig()));
	}
}
