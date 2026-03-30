using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Minimal UI Toolkit shop: lists items from <see cref="ShopRepository"/>, purchases via <see cref="ShopPurchase"/>,
/// and exposes Continue for the next round (same as <see cref="ShopController"/> N key).
/// </summary>
public class ShopPanelPresenter : MonoBehaviour, IStatePresenter<ShopSessionViewData>
{
	[SerializeField] VisualTreeAsset shopPanelUxml;
	[SerializeField] UIDocument uiDocument;

	Label _goldLabel;
	VisualElement _itemList;
	Button _continueButton;

	PurchaseContext _purchaseContext;

	PanelSettings _runtimePanelSettings;
	bool _ownsPanelSettings;
	VisualElement _shopRoot;

	bool _pendingContinue;
	bool _continueBound;

	/// <summary>
	/// Avoids clearing/rebuilding the item list every frame; UITK buttons need stable elements to receive clicks.
	/// </summary>
	bool _hasLastRenderedSnapshot;
	ShopSessionViewData _lastRenderedSnapshot;

	public event Action OnSuccessfulPurchase;

	public void BindSession(GameConfig runtime)
	{
		_purchaseContext = new PurchaseContext(runtime.playerInventory, runtime.playerWallet);
		_hasLastRenderedSnapshot = false;
	}

	public void SetShopVisible(bool visible)
	{
		EnsureRoot();
		if (_shopRoot == null)
			return;
		_shopRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
	}

	public void Render(ShopSessionViewData data)
	{
		EnsureRoot();
		if (_goldLabel == null || _itemList == null)
			return;

		_goldLabel.text = $"Gold: {data.Gold}";

		if (_hasLastRenderedSnapshot && ViewDataEquals(_lastRenderedSnapshot, data))
			return;

		_lastRenderedSnapshot = data;
		_hasLastRenderedSnapshot = true;

		_itemList.Clear();
		var rows = data.Rows;
		for (int i = 0; i < rows.Count; i++)
			_itemList.Add(BuildRow(rows[i]));
	}

	static bool ViewDataEquals(ShopSessionViewData a, ShopSessionViewData b)
	{
		if (a.Gold != b.Gold)
			return false;
		if (a.Rows.Count != b.Rows.Count)
			return false;
		for (int i = 0; i < a.Rows.Count; i++)
		{
			ShopOfferRowViewData ra = a.Rows[i];
			ShopOfferRowViewData rb = b.Rows[i];
			if (ra.Purchasable != rb.Purchasable || ra.CanBuy != rb.CanBuy || ra.Price != rb.Price)
				return false;
			if (ra.DisplayName != rb.DisplayName || ra.Description != rb.Description)
				return false;
		}
		return true;
	}

	public bool ConsumeContinueRequested()
	{
		if (!_pendingContinue)
			return false;
		_pendingContinue = false;
		return true;
	}

	void Awake()
	{
		if (uiDocument == null)
			uiDocument = GetComponent<UIDocument>();
		if (uiDocument == null)
			uiDocument = gameObject.AddComponent<UIDocument>();

		if (uiDocument.panelSettings == null)
		{
			_runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			_runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			_runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
			uiDocument.panelSettings = _runtimePanelSettings;
			_ownsPanelSettings = true;
		}

		if (shopPanelUxml != null)
			uiDocument.visualTreeAsset = shopPanelUxml;

		uiDocument.sortingOrder = 100;
	}

	void OnDestroy()
	{
		if (_ownsPanelSettings && _runtimePanelSettings != null)
			Destroy(_runtimePanelSettings);
	}

	void Start()
	{
		EnsureRoot();
		SetShopVisible(false);
	}

	void EnsureRoot()
	{
		if (_shopRoot != null || uiDocument == null)
			return;

		var tree = uiDocument.rootVisualElement;
		if (tree == null)
			return;

		_shopRoot = tree.Q<VisualElement>("shop-root");
		_goldLabel = tree.Q<Label>("gold-label");
		_itemList = tree.Q<VisualElement>("item-list");
		_continueButton = tree.Q<Button>("continue-button");
		TryBindContinue();
	}

	void TryBindContinue()
	{
		if (_continueBound || _continueButton == null)
			return;
		_continueButton.clicked += () => { _pendingContinue = true; };
		_continueBound = true;
	}

	VisualElement BuildRow(ShopOfferRowViewData row)
	{
		IPurchasable purchasable = (IPurchasable)(object)row.Purchasable;

		var ve = new VisualElement();
		ve.AddToClassList("shop-row");

		var header = new VisualElement();
		header.AddToClassList("shop-row-header");
		var nameLabel = new Label(row.DisplayName);
		nameLabel.AddToClassList("shop-row-name");
		var priceLabel = new Label($"{row.Price} g");
		priceLabel.AddToClassList("shop-row-price");
		header.Add(nameLabel);
		header.Add(priceLabel);

		var desc = new Label(row.Description);
		desc.AddToClassList("shop-row-desc");

		var actions = new VisualElement();
		actions.AddToClassList("shop-row-actions");
		var buy = new Button(() => TryBuy(purchasable)) { text = "Buy" };
		buy.AddToClassList("shop-buy");
		buy.SetEnabled(row.CanBuy);
		actions.Add(buy);

		ve.Add(header);
		ve.Add(desc);
		ve.Add(actions);
		return ve;
	}

	void TryBuy(IPurchasable purchasable)
	{
		if (!ShopPurchase.TryPurchase(purchasable, _purchaseContext))
			return;
		OnSuccessfulPurchase?.Invoke();
	}
}
