using BridgeOfBlood.Data.Spells;
using UnityEngine;
using UnityEngine.UI;

public class SpellGemPresenter : MonoBehaviour
{
    [SerializeField] Image iconImage;
    public void Present(RuntimeSpellItem spellItem)
    {
        iconImage.sprite = spellItem.spellItem.ShopItemDefinition.Sprite;
    }
}
