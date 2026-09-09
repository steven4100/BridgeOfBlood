using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Data.Shop
{
	public sealed class PurchaseContext
	{
		public IInventoryService Inventory { get; }
		public IWalletService Wallet { get; }
		public ISpellInventoryService SpellInventory { get; }

		/// <summary>
		/// Set by <see cref="ShopPurchase.TryPurchase"/> around <see cref="IPurchasable.OnPurchase"/>.
		/// </summary>
		public ShopItemDefinition CurrentShopListing { get; set; }

		public PurchaseContext(IInventoryService inventory, IWalletService wallet, ISpellInventoryService spellInventory)
		{
			Inventory = inventory;
			Wallet = wallet;
			SpellInventory = spellInventory;
		}
	}

	public interface IPurchasable : IRandomElement
	{
		/// <summary>
		/// Shop metadata for this listing (required for shop inventory and weighted selection).
		/// </summary>
		ShopItemDefinition ShopItemDefinition { get; }

		void OnPurchase(PurchaseContext context);

		/// <summary>
		/// Whether <see cref="OnPurchase"/> can complete with this context (e.g. stash has a free cell).
		/// Checked by <see cref="ShopPurchase.TryPurchase"/> before spending gold.
		/// </summary>
		bool CanPurchase(PurchaseContext context);
	}

	public interface ISpellTargetPurchasable : IPurchasable
	{
		public bool CanBeApplied(RuntimeSpell spell);
		public bool PurchaseAndApplyToSpell(RuntimeSpell spell);
	}
}
