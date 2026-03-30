using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Data.Shop
{
	public sealed class PurchaseContext
	{
		public PlayerInventory Inventory { get; }
		public PlayerWallet Wallet { get; }

		/// <summary>
		/// Set by <see cref="ShopPurchase.TryPurchase"/> around <see cref="IPurchasable.OnPurchase"/> so payloads can copy listing economy into <see cref="InventoryItem"/>.
		/// </summary>
		public ShopItemDefinition CurrentShopListing { get; set; }

		public PurchaseContext(PlayerInventory inventory, PlayerWallet wallet)
		{
			Inventory = inventory;
			Wallet = wallet;
		}

		public void AddPurchasedPayload(IInventoryItem payload, int stackCount = 1)
		{
			ShopItemDefinition listing = CurrentShopListing;
			Inventory.AddPayload(payload, stackCount, listing.ResellValue, listing.IsResellable);
		}
	}

	public interface IPurchasable : IRandomElement
	{
		/// <summary>
		/// Shop metadata for this listing (required for shop inventory and weighted selection).
		/// </summary>
		ShopItemDefinition ShopItemDefinition { get; }

		void OnPurchase(PurchaseContext context);
	}
}
