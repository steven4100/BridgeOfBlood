using UnityEngine;
using BridgeOfBlood.Effects;
using UnityEngine.EventSystems;

public class PurchasableItemGemUI : PurchasableItemUI, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    Gem gem;
    [SerializeField]
    EffectDescriptionTooltip effectDescriptionTooltip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        effectDescriptionTooltip.DisplayEffectDescription(gem);
        HoverTooltip.Current.Show(this, (RectTransform)effectDescriptionTooltip.transform, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverTooltip.Current.Hide(this);
    }

    public void SetGem(Gem gem)
    {
        this.gem = gem;
    }
}
