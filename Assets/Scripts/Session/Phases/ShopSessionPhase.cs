using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using UnityEngine.SceneManagement;

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
    }

    void OnShopContinueButtonPressed(ref ShopContinueButtonPressed @event)
	{
		controller.SetState(SessionState.Round);
	}


    public override void Tick(SessionFlowContext context, float deltaTime)
    {
        
    }

}
