using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class ShopSessionPhase : SessionPhaseBase
{
	public ShopSessionPhase(SessionFlowController controller) : base(controller)
	{
	}

	protected override void OnEnter(SessionFlowContext context)
	{
		SharedGameEventBus.Bus.SubscribeTo<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.Raise(new ShopEnterEvent());
	}

	protected override void OnExit(SessionFlowContext context)
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<ShopContinueButtonPressed>(OnShopContinueButtonPressed);
		SharedGameEventBus.Bus.Raise(new ShopExitEvent());
	}

	void OnShopContinueButtonPressed(ref ShopContinueButtonPressed @event)
	{
		controller.SetState(SessionState.Round);
	}

	public override void Tick(SessionFlowContext context, float deltaTime)
	{
		if (Input.GetKeyDown(KeyCode.N))
			controller.SetState(SessionState.Round);
	}
}
