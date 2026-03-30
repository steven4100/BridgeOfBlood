namespace BridgeOfBlood.Data.Shop
{
	public static class ShopPurchase
	{
		public static bool TryPurchase(IPurchasable purchasable, PurchaseContext context)
		{
			ShopItemDefinition def = purchasable.ShopItemDefinition;
			int price = def.Price;
			if (price > 0 && !context.Wallet.TrySpend(price))
				return false;

			context.CurrentShopListing = def;
			try
			{
				purchasable.OnPurchase(context);
			}
			finally
			{
				context.CurrentShopListing = null;
			}

			return true;
		}
	}
}
