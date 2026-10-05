using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using UnityEngine;

/// <summary>
/// Shop listing for a map-modifier placement visit of <see cref="placementCount"/> modifiers.
/// Purchase raises <see cref="MapModPlacementRequested"/>; the shop phase starts the visit.
/// </summary>
[CreateAssetMenu(
    fileName = "MapModifierPlacementTicket",
    menuName = "Bridge of Blood/Items/Map Modifier Placement Ticket")]
public sealed class MapModifierPlacementTicket : ScriptableObject, IPurchasable
{
    [SerializeField] ShopItemDefinition shopItemDefinition;
    [SerializeField] int placementCount = 1;

    public ShopItemDefinition ShopItemDefinition => shopItemDefinition;
    public int PlacementCount => placementCount;

    float IRandomElement.Weight
    {
        get => ((IRandomElement)shopItemDefinition).Weight;
        set => ((IRandomElement)shopItemDefinition).Weight = value;
    }

    public bool CanPurchase(PurchaseContext context) => placementCount > 0;

    public void OnPurchase(PurchaseContext context)
    {
        SharedGameEventBus.Bus.Raise(new MapModPlacementRequested
        {
            placementCount = placementCount
        });
    }
}
