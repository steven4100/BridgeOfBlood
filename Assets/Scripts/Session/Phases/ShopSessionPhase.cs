using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;

public sealed class ShopSessionPhase : SessionPhaseBase<ShopSessionViewData>
{
	readonly ShopPanelPresenter _shopPanel;
	Action _onPurchase;

	public ShopSessionPhase(ShopPanelPresenter shopPanel)
		: base(shopPanel != null ? shopPanel : ShopSessionNoOpPresenter.Instance)
	{
		_shopPanel = shopPanel;
	}

	public override void Enter(SessionFlowContext context)
	{
		_onPurchase = () =>
		{
			context.SpellCollection.SyncSpellLoopFromInventory(context.RuntimeGameConfig.playerInventory.GetSpellLoopAuthoring());
		};

		if (_shopPanel != null)
		{
			_shopPanel.BindSession(context.RuntimeGameConfig);
			_shopPanel.OnSuccessfulPurchase += _onPurchase;
			_shopPanel.SetShopVisible(true);
		}
	}

	public override void Exit(SessionFlowContext context)
	{
		if (_shopPanel != null)
		{
			_shopPanel.OnSuccessfulPurchase -= _onPurchase;
			_shopPanel.SetShopVisible(false);
		}
		_onPurchase = null;
	}

	protected override ShopSessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		bool requestNext = context.ShopController.Tick().requestedNextRound;
		if (_shopPanel != null && _shopPanel.ConsumeContinueRequested())
			requestNext = true;

		if (requestNext)
		{
			context.Flow.SetState(SessionState.Round);
			context.RoundController.StartNextRound();
			context.ResetForNewRound();
		}

		return BuildShopViewData(context);
	}

	static ShopSessionViewData BuildShopViewData(SessionFlowContext context)
	{
		GameConfig cfg = context.RuntimeGameConfig;
		var repo = new ShopRepository(cfg.shopConfig);
		PlayerInventory inv = cfg.playerInventory;
		PlayerWallet wallet = cfg.playerWallet;
		int gold = wallet.gold;
		var rows = new List<ShopOfferRowViewData>();
		IReadOnlyList<IPurchasable> items = repo.GetAll();
		for (int i = 0; i < items.Count; i++)
		{
			IPurchasable p = items[i];
			if (p is IInventoryItem asset && inv.OwnsPayload(asset))
				continue;
			ShopItemDefinition def = p.ShopItemDefinition;
			string desc = string.IsNullOrEmpty(def.Description) ? " " : def.Description;
			bool canBuy = wallet.gold >= def.Price;
			var so = (UnityEngine.ScriptableObject)(object)p;
			rows.Add(new ShopOfferRowViewData(def.DisplayName, desc, def.Price, canBuy, so));
		}
		return new ShopSessionViewData(gold, rows);
	}
}
