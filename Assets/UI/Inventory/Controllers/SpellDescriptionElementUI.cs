using UnityEngine;
using TMPro;

public class SpellDescriptionElementUI : MonoBehaviour
{
    [SerializeField]
    TMP_Text text;
    public void RenderSpellDescription(string description)
    {
        text.text = description;
    }
}
