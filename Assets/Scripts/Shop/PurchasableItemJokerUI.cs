using UnityEngine;
using BridgeOfBlood.Effects;
using UnityEngine.EventSystems;
public class PurchasableItemJokerUI : PurchasableItemUI, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] 
    Item item;
    [SerializeField]
    EffectDescriptionTooltip effectDescriptionTooltip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        effectDescriptionTooltip.DisplayEffectDescription(item);
        HoverTooltip.Current.Show(this, (RectTransform)effectDescriptionTooltip.transform, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverTooltip.Current.Hide(this);
    }

    public void SetJoker(Item joker)
    {
        item = joker;
    }
}
