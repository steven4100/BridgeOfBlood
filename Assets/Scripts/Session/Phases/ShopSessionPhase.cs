using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class ShopSessionPhase : SessionPhaseBase
{
	SessionFlowContext _context;

	public ShopSessionPhase(SessionFlowController controller) : base(controller)
	{
	}

	protected override void OnEnter(SessionFlowContext context)
	{
		_context = context;
		SharedGameEventBus.Bus.SubscribeTo<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.SubscribeTo<MapModPlacementRequested>(OnMapModPlacementRequested);
		SharedGameEventBus.Bus.Raise(new ShopEnterEvent());
	}

	protected override void OnExit(SessionFlowContext context)
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.UnsubscribeFrom<MapModPlacementRequested>(OnMapModPlacementRequested);
		SharedGameEventBus.Bus.Raise(new ShopExitEvent());
	}

	void OnShopContinueButtonPressed(ref ShopContinueButtonPressed @event)
	{
		controller.SetState(SessionState.Round);
	}

	void OnMapModPlacementRequested(ref MapModPlacementRequested @event)
	{
		_context.AllowedMapPlacements = @event.placementCount;
		controller.SetState(SessionState.MapMod);
	}

	public override void Tick(SessionFlowContext context, float deltaTime)
	{
		if (Input.GetKeyDown(KeyCode.N))
			controller.SetState(SessionState.Round);
	}
}
