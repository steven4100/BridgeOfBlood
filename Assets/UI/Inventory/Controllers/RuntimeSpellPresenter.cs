using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;

public enum ShopSpellHighlight
{
    None,
    NotApplicable,
    Applicable,
    /// <summary>Optional stronger feedback (e.g. hover or pre-purchase emphasis).</summary>
    Selected,
}

/// <summary>
/// Single tile inside the spell inventory strip. Carries the runtime <see cref="SpellId"/> so the
/// containing <see cref="SpellInventoryController"/> can read sibling order back as a list of ids
/// after a drag-reorder.
/// </summary>
public class RuntimeSpellPresenter : MonoBehaviour
{
    static readonly Color IconBaseColor = Color.white;
    static readonly Color ApplicableTint = new Color(0.65f, 1f, 0.65f, 1f);
    static readonly Color SelectedTint = new Color(0.4f, 1f, 0.45f, 1f);
    static readonly Color NotApplicableTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField] Image highlightImage;
    [SerializeField] Image iconImage;
    [SerializeField] SpellGemPresenter GemPf;
    [SerializeField] Transform GemLayoutRoot;
    [SerializeField] Button spellClickButton;

    public int SpellId { get; private set; }

    private List<SpellGemPresenter> gems = new List<SpellGemPresenter>();
    private Action<int> spellTileClickHandler;
    private ShopSpellHighlight shopHighlight = ShopSpellHighlight.None;

    void Awake()
    {
        if (spellClickButton == null)
            spellClickButton = GetComponent<Button>();

        if (spellClickButton != null)
        {
            spellClickButton.onClick.AddListener(OnSpellClickButtonClicked);
            spellClickButton.interactable = false;
        }
    }

    void OnDestroy()
    {
        if (spellClickButton != null)
            spellClickButton.onClick.RemoveListener(OnSpellClickButtonClicked);
    }

    void OnSpellClickButtonClicked()
    {
        spellTileClickHandler?.Invoke(SpellId);
    }

    public void SetSpellTileClickHandler(Action<int> handler)
    {
        spellTileClickHandler = handler;
        if (spellClickButton != null)
            spellClickButton.interactable = handler != null;
    }

    public void SetShopHighlight(ShopSpellHighlight highlight)
    {
        shopHighlight = highlight;

        switch (highlight)
        {
            case ShopSpellHighlight.Applicable:
                highlightImage.color = ApplicableTint;
                break;
            case ShopSpellHighlight.Selected:
                highlightImage.color = SelectedTint;
                break;
            case ShopSpellHighlight.NotApplicable:
                highlightImage.color = NotApplicableTint;
                break;
            default:
                highlightImage.color = IconBaseColor;
                break;
        }
    }

    public void Bind(RuntimeSpell spell)
    {
        SpellId = spell.spellId;
        iconImage.sprite = spell.Definition.icon;
        gameObject.name = $"Spell_{spell.Definition.name}";
        shopHighlight = ShopSpellHighlight.None;
        iconImage.color = IconBaseColor;
        PresentGems(spell);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void SetGemsDefaultVisibility(bool visibility)
    {
        GemLayoutRoot.gameObject.SetActive(visibility);
    }

    private void PresentGems(RuntimeSpell spell)
    {
        foreach (SpellGemPresenter gem in gems)
        {
            Destroy(gem.gameObject);
        }
        gems.Clear();
        for (int i = 0; i < spell.numGemSlots; i++)
        {
            SpellGemPresenter gem = Instantiate(GemPf, GemLayoutRoot);
            gems.Add(gem);
            if(i < spell.spellItems.Count)
                gem.Present(spell.spellItems[i]);
        }
    }
}
