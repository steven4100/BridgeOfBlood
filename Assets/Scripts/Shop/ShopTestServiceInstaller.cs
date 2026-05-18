using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene bootstrapper for the shop test scene. Must execute before ShopController (DefaultExecutionOrder -100).
///
/// Two modes:
///   - Pure mock: leave all SO fields empty. Registers in-memory mocks with no Unity asset dependencies.
///   - Real assets: assign PlayerInventory, PlayerWallet (and optionally ShopConfig) in the Inspector.
///     The SO instances are registered via ServiceLocator.RegisterInstance so the service locator
///     resolves them without needing a parameterless constructor.
///
/// Starting spells for the spell strip:
///   - With <see cref="PlayerInventory"/>: set <c>startingSpells</c> / <c>startingNumberOfSpells</c> on that asset; this installer calls
///     <see cref="PlayerInventory.RebuildFromStartingDefinition"/> before registering services.
///   - Mock inventory only: assign spells under <see cref="startingSpellsMock"/>.
///
/// Starting passive items for the item strip:
///   - With <see cref="PlayerInventory"/>: set <c>startingItems</c> on that asset (same rebuild as above).
///   - Mock inventory only: assign <see cref="startingItemsMock"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ShopTestServiceInstaller : MonoBehaviour
{
    [Header("Shop Service")]
    [SerializeField] private MockShopService mockShopService;
    [SerializeField] private ShopConfig shopConfig;

    [Header("Optional – leave empty to use pure mocks")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerWallet wallet;

    [Header("Spell strip (mock inventory only)")]
    [Tooltip("Used when Player Inventory is not assigned. Ignored when using a PlayerInventory asset.")]
    [SerializeField] private List<SpellAuthoringData> startingSpellsMock = new List<SpellAuthoringData>();

    [Header("Item strip (mock inventory only)")]
    [Tooltip("Used when Player Inventory is not assigned. Ignored when using a PlayerInventory asset.")]
    [SerializeField] private List<Item> startingItemsMock = new List<Item>();

    private void Awake()
    {
        if (inventory != null)
            inventory.RebuildFromStartingDefinition();

        RegisterInventoryService();
        RegisterSpellInventoryService();
        RegisterWalletService();
        RegisterShopService();
    }

    private void RegisterInventoryService()
    {
        if (ServiceAlreadyRegistered<IInventoryService>())
            return;

        if (inventory != null)
        {
            ServiceLocator.Current.RegisterInstance<IInventoryService>(inventory);
            return;
        }

        var mock = new MockInventoryService();
        if (startingItemsMock != null)
        {
            for (int i = 0; i < startingItemsMock.Count; i++)
            {
                Item item = startingItemsMock[i];
                if (item == null) continue;
                mock.AddInventoryItem(new InventoryItem(item));
            }
        }

        ServiceLocator.Current.RegisterInstance<IInventoryService>(mock);
    }

    private void RegisterSpellInventoryService()
    {
        if (ServiceAlreadyRegistered<ISpellInventoryService>())
            return;

        if (inventory != null)
        {
            ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(inventory.SpellCollection);
            return;
        }

        var spellList = startingSpellsMock;
        if (spellList != null && spellList.Count > 0)
        {
            ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(new SpellCollection(spellList));
            return;
        }

        ServiceLocator.Current.RegisterInstance<ISpellInventoryService>(new SpellCollection(null));
    }

    private void RegisterWalletService()
    {
        if (ServiceAlreadyRegistered<IWalletService>())
            return;

        if (wallet != null)
            ServiceLocator.Current.RegisterInstance<IWalletService>(wallet);
        else
            ServiceLocator.Current.For<IWalletService>().Use<MockWalletService>();
    }

    private void RegisterShopService()
    {
        if (ServiceAlreadyRegistered<IShopService>())
            return;

        if (mockShopService != null)
        {
            ServiceLocator.Current.RegisterInstance<IShopService>(mockShopService);
        }
        else if (shopConfig != null)
        {
            var repo = new ShopRepository(shopConfig);
            ServiceLocator.Current.RegisterInstance<IShopService>(new RepositoryShopService(repo, inventory));
        }
        else
        {
            ServiceLocator.Current.RegisterInstance<IShopService>(new MockShopService());
        }
    }

    private static bool ServiceAlreadyRegistered<TService>() where TService : class
        => ServiceLocator.Current.GetService<TService>(throwError: false) != null;
}
