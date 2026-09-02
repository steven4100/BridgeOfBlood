using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using UnityEngine;
using UnityEngine.UI;

public struct ReceptacleDropContext
{
	public bool Commit;
	public bool UsePointer;
	public IInventoryOccupant Relocating;
	public int SourceIndex;
	public Vector2 ScreenPosition;
	public Camera EventCamera;
	public int InsertIndex;
	public Vector2Int Cell;
}

public interface IItemReceptacle
{
	bool VisitSpell(RuntimeSpell spell, ref ReceptacleDropContext ctx);
	bool VisitGem(RuntimeGem gem, ref ReceptacleDropContext ctx);
	bool VisitItem(RuntimeItem item, ref ReceptacleDropContext ctx);
	bool TryRemove(IInventoryOccupant occupant);
	void SetDropPreview(bool? valid);
}

public static class InventoryOccupantDispatch
{
	public static bool Accept(this IItemReceptacle receptacle, IInventoryOccupant occupant, ref ReceptacleDropContext ctx)
	{
		switch (occupant)
		{
			case RuntimeSpell spell: return receptacle.VisitSpell(spell, ref ctx);
			case RuntimeGem gem: return receptacle.VisitGem(gem, ref ctx);
			case RuntimeItem item: return receptacle.VisitItem(item, ref ctx);
			default: return false;
		}
	}
}

public static class InventoryDropSite
{
	public static int HorizontalStripInsertIndex(RectTransform strip, Vector2 screenPosition, Camera camera)
	{
		if (strip == null)
			return 0;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(strip, screenPosition, camera, out Vector2 localPoint))
			return 0;

		int newIndex = 0;
		int count = strip.childCount;
		for (int i = 0; i < count; i++)
		{
			var child = strip.GetChild(i) as RectTransform;
			if (child == null || !child.gameObject.activeInHierarchy)
				continue;
			Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(strip, child);
			if (localPoint.x > b.center.x)
				newIndex = i + 1;
		}
		return Mathf.Clamp(newIndex, 0, count);
	}

	public static int StripInsertIndex(RectTransform strip, in ReceptacleDropContext ctx)
	{
		if (!ctx.UsePointer)
			return ctx.InsertIndex;

		int index = HorizontalStripInsertIndex(strip, ctx.ScreenPosition, ctx.EventCamera);
		if (ctx.SourceIndex >= 0 && index > ctx.SourceIndex)
			index--;
		return index;
	}
}

public static class InventoryDropPreview
{
	static readonly Color ValidTint = new Color(0.55f, 1f, 0.55f, 0.85f);
	static readonly Color InvalidTint = new Color(1f, 0.45f, 0.45f, 0.85f);

	public static void EnsureRaycastGraphic(GameObject host)
	{
		if (host.GetComponent<Graphic>() != null)
			return;
		var image = host.AddComponent<Image>();
		image.color = new Color(1f, 1f, 1f, 0.01f);
		image.raycastTarget = true;
	}

	public static void Apply(Image image, ref Color baseColor, bool? valid)
	{
		if (image == null)
			return;
		if (valid == null)
		{
			image.color = baseColor;
			return;
		}
		image.color = valid.Value ? ValidTint : InvalidTint;
	}
}
