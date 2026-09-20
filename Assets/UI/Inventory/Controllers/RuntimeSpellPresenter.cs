using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine.EventSystems;

public enum ShopSpellHighlight
{
    None,
    NotApplicable,
    Applicable,
    /// <summary>Optional stronger feedback (e.g. hover or pre-purchase emphasis).</summary>
    Selected,
}

/// <summary>
/// Single tile inside the spell inventory strip.
/// </summary>
public class RuntimeSpellPresenter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    static readonly Color IconBaseColor = Color.white;
    static readonly Color ApplicableTint = new Color(0.65f, 1f, 0.65f, 1f);
    static readonly Color SelectedTint = new Color(0.4f, 1f, 0.45f, 1f);
    static readonly Color NotApplicableTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField] SpellDescriptionUI spellDescriptionUI;
    [SerializeField] Image highlightImage;
    [SerializeField] Image iconImage;
    [SerializeField] SpellGemPresenter GemPf;
    [SerializeField] Transform GemLayoutRoot;
    [SerializeField] Button spellClickButton;

    public int SpellId { get; private set; }
    public RuntimeSpell Spell { get; private set; }

    private List<SpellGemPresenter> gems = new List<SpellGemPresenter>();
    private Action<int> spellTileClickHandler;
    private ShopSpellHighlight shopHighlight = ShopSpellHighlight.None;
    ISpellModificationService _spellModificationService;

    void OnEnable()
    {
        ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
    }

    void OnDisable()
    {
        ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
        HoverTooltip tooltip = HoverTooltip.Current;
        if (tooltip != null)
            tooltip.Hide(this);
    }

    void OnServicesRegistered(ref ServicesRegisteredEvent _)
    {
        _spellModificationService = ServiceLocator.Current.GetService<ISpellModificationService>();
    }

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

    public void Bind(RuntimeSpell spell, IItemReceptacle source, int index)
    {
        Spell = spell;
        SpellId = spell.spellId;
        iconImage.sprite = spell.Definition.icon;
        gameObject.name = $"Spell_{spell.Definition.name}";
        shopHighlight = ShopSpellHighlight.None;
        iconImage.color = IconBaseColor;
        InventoryDragHandle.Bind(gameObject, spell, source, index, default);
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
        for (int i = 0; i < spell.Gems.SlotCount; i++)
        {
            SpellGemPresenter gem = Instantiate(GemPf, GemLayoutRoot);
            gems.Add(gem);
            gem.Bind(spell.Gems, i);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ModificationSet mods = _spellModificationService.GetSpellModCollectionForSpell(Spell);
        spellDescriptionUI.RenderSpell(Spell.Definition, mods);
        HoverTooltip.Current.Show(this, (RectTransform)spellDescriptionUI.transform, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverTooltip.Current.Hide(this);
    }
}
