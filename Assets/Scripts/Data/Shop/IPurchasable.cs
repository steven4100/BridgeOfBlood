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
		/// Set by <see cref="ShopPurchase.TryPurchase"/> around <see cref="IPurchasable.OnPurchase"/> so payloads can copy listing economy into <see cref="InventoryItem"/> or apply spell grants.
		/// </summary>
		public ShopItemDefinition CurrentShopListing { get; set; }

		/// <summary>
		/// When purchasing a spell-target shop listing, set to the runtime spell that receives the gem before <see cref="ShopPurchase.TryPurchase"/>.
		/// Cleared in <see cref="ShopPurchase.TryPurchase"/> finally.
		/// </summary>
		public RuntimeSpell SpellGemTarget { get; set; }

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
	}

	public interface ISpellTargetPurchasable : IPurchasable
	{
		public bool CanBeApplied(RuntimeSpell spell);
		public bool PurchaseAndApplyToSpell(RuntimeSpell spell);
	}
}
