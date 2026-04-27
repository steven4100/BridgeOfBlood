using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single tile inside the spell inventory strip. Carries the runtime <see cref="SpellId"/> so the
/// containing <see cref="SpellInventoryController"/> can read sibling order back as a list of ids
/// after a drag-reorder.
/// </summary>
public class RuntimeSpellPresenter : MonoBehaviour
{
    [SerializeField] Image iconImage;

    public int SpellId { get; private set; }

    public void Bind(in RuntimeSpellUiDTO dto)
    {
        SpellId = dto.id;
        iconImage.sprite = dto.icon;
        gameObject.name = $"Spell_{dto.name}";
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}
