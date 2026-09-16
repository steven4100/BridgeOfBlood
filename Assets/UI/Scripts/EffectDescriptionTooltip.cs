using BridgeOfBlood.Effects;
using TMPro;
using UnityEngine;

public class EffectDescriptionTooltip : MonoBehaviour
{
    public TMP_Text text;

    public void DisplayEffectDescription(IEffect effect)
    {
        text.text = effect.GetDescription();
    }
}
