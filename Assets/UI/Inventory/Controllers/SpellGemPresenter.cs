using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using UnityEngine;
using UnityEngine.UI;

public class SpellGemPresenter : MonoBehaviour, IItemReceptacle
{
    [SerializeField] Image iconImage;

    RuntimeSpellGemCollection _collection;
    int _slotIndex;
    Color _dropPreviewBase = Color.white;

    public void Bind(RuntimeSpellGemCollection collection, int slotIndex)
    {
        _collection = collection;
        _slotIndex = slotIndex;
        _dropPreviewBase = iconImage.color;
        RuntimeGem gem = collection.GetSlot(slotIndex);
        if (gem != null)
        {
            iconImage.sprite = gem.GhostSprite;
            iconImage.color = Color.white;
            _dropPreviewBase = iconImage.color;
            InventoryDragHandle.Bind(gameObject, gem, this, slotIndex, default);
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color = new Color(0.47f, 0.47f, 0.47f, 1f);
            _dropPreviewBase = iconImage.color;
            InventoryDragHandle.Clear(gameObject);
        }
    }

    public void SetDropPreview(bool? valid)
    {
        InventoryDropPreview.Apply(iconImage, ref _dropPreviewBase, valid);
    }

    public bool VisitSpell(RuntimeSpell spell, ref ReceptacleDropContext ctx) => false;

    public bool VisitGem(RuntimeGem gem, ref ReceptacleDropContext ctx)
    {
        int index = ctx.UsePointer ? _slotIndex : ctx.InsertIndex;
        if (index < 0 || index >= _collection.SlotCount)
            index = _collection.FirstEmptySlot;
        ctx.InsertIndex = index;
        RuntimeGem relocating = ctx.Relocating as RuntimeGem;
        if (!_collection.CanPlace(gem, index, relocating))
            return false;
        if (!ctx.Commit)
            return true;
        return _collection.TryInsert(gem, index);
    }

    public bool VisitItem(RuntimeItem item, ref ReceptacleDropContext ctx) => false;

    public bool TryRemove(IInventoryOccupant occupant)
    {
        return occupant is RuntimeGem gem && _collection.TryRemove(gem);
    }
}
