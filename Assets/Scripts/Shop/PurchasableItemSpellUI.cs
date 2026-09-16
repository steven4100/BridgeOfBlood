using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;
using UnityEngine.EventSystems;

public class PurchasableItemSpellUI : PurchasableItemUI, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] public SpellDescriptionUI spellDescriptionUI;
    SpellAuthoringData spellAuthoringData;

    public void SetSpell(SpellAuthoringData spellAuthoringData)
    {
        this.spellAuthoringData = spellAuthoringData;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        spellDescriptionUI.RenderSpell(spellAuthoringData, new SpellModifications());
        HoverTooltip.Current.Show(this, (RectTransform)spellDescriptionUI.transform, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverTooltip.Current.Hide(this);
    }
}
