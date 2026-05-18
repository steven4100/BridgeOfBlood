using BridgeOfBlood.Data.Shop;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PurchasableItemUI : MonoBehaviour
{
    public TMP_Text PriceText;
    public Button ItemSelectedButton;
    public Button ConfirmPurchaseButton;

    public Action<PurchasableItemUI> UserPurchaseConfirmed;

    protected IPurchasable purchasable;
    protected PurchaseContext purchaseContext;
    protected ShopController shopController;

    private void OnEnable()
    {
        ItemSelectedButton.onClick.AddListener(OnItemClicked);
        ConfirmPurchaseButton.onClick.AddListener(OnConfirmPurchaseClicked);
    }

    private void OnDisable()
    {
        ItemSelectedButton.onClick.RemoveListener(OnItemClicked);
        ConfirmPurchaseButton.onClick.RemoveListener(OnConfirmPurchaseClicked);
    }

    public void SetPurchasable(IPurchasable purchasable, PurchaseContext purchaseContext, ShopController shopController)
    {
        this.purchasable = purchasable;
        this.purchaseContext = purchaseContext;
        this.shopController = shopController;
        Present();
    }

    protected virtual void Present()
    {
        PriceText.text = purchasable.ShopItemDefinition.Price.ToString();
        ConfirmPurchaseButton.gameObject.SetActive(false);
    }

    public void ShowConfirmPurchaseButton()
    {
        ConfirmPurchaseButton.gameObject.SetActive(true);
    }

    public void HideConfirmPurchaseButton()
    {
        ConfirmPurchaseButton.gameObject.SetActive(false);
    }

    protected virtual void OnItemClicked()
    {
        shopController.FocusRegularRow(this);
    }

    protected virtual void OnConfirmPurchaseClicked()
    {
        shopController.TryPurchase(purchasable);
    }
}
