using TMPro;
using UnityEngine;
using BridgeOfBlood.Data.Spells;
using System.Collections.Generic;

public class SpellDescriptionUI : MonoBehaviour
{
    public SpellDescriptionElementUI spellDescriptionElementPF;
    public Transform root;

    List<SpellDescriptionElementUI> elements = new List<SpellDescriptionElementUI>();
    
    public void RenderSpell(SpellAuthoringData spell, SpellModifications spellModifications)
    {
        Clear();
        AttackEntityData attackEntityData = spell.AttackEntityData;
        foreach (AttackEntityBehavior behavior in attackEntityData.GetAllBehaviors())
        {
            if (behavior is not IPresentableSpellAttribute presentableSpellAttribute) continue;
            string description = presentableSpellAttribute.GetDisplayDescription(spellModifications, spell.attributeMask);
            SpellDescriptionElementUI element = Instantiate(spellDescriptionElementPF, root);
            element.RenderSpellDescription(description);
            elements.Add(element);
        }
    }

    private void Clear()
    {
        foreach (SpellDescriptionElementUI element in elements)
        {
            Destroy(element.gameObject);
        }
        elements.Clear();
    }
}
