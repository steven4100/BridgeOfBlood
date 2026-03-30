using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One row in the shop list (data-only; purchase target is the underlying <see cref="IPurchasable"/> asset).
/// </summary>
public readonly struct ShopOfferRowViewData
{
	public readonly string DisplayName;
	public readonly string Description;
	public readonly int Price;
	public readonly bool CanBuy;
	public readonly ScriptableObject Purchasable;

	public ShopOfferRowViewData(string displayName, string description, int price, bool canBuy, ScriptableObject purchasable)
	{
		DisplayName = displayName;
		Description = description;
		Price = price;
		CanBuy = canBuy;
		Purchasable = purchasable;
	}
}

/// <summary>
/// Full shop panel snapshot for one session tick.
/// </summary>
public readonly struct ShopSessionViewData
{
	public readonly int Gold;
	public readonly List<ShopOfferRowViewData> Rows;

	public ShopSessionViewData(int gold, List<ShopOfferRowViewData> rows)
	{
		Gold = gold;
		Rows = rows;
	}
}
