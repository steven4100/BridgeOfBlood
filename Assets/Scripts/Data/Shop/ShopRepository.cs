using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Shop
{
	public class ShopRepository
	{
		readonly IPurchasable[] _allItems;
		readonly Dictionary<ShopItemType, List<IPurchasable>> _byType;
		readonly ShopConfig _config;

		public ShopRepository(ShopConfig config)
		{
			_config = config;
			_allItems = CollectPurchasables();
			_byType = new Dictionary<ShopItemType, List<IPurchasable>>();

			for (int i = 0; i < _allItems.Length; i++)
			{
				IPurchasable p = _allItems[i];
				ShopItemDefinition shop = p.ShopItemDefinition;

				if (!_byType.TryGetValue(shop.ShopItemType, out var list))
				{
					list = new List<IPurchasable>();
					_byType[shop.ShopItemType] = list;
				}
				list.Add(p);
			}
		}

		/// <summary>
		/// Discovers every <see cref="IPurchasable"/> under any <c>Resources</c> folder (path null searches the whole tree).
		/// </summary>
		static IPurchasable[] CollectPurchasables()
		{
			ScriptableObject[] assets = Resources.LoadAll<ScriptableObject>("");
			var merged = new List<IPurchasable>(assets.Length);
			for (int i = 0; i < assets.Length; i++)
			{
				if (assets[i] is IPurchasable p)
					merged.Add(p);
			}
			return merged.ToArray();
		}

		public IReadOnlyList<IPurchasable> GetAll() => _allItems;

		public IReadOnlyList<IPurchasable> GetItemsByType(ShopItemType type)
		{
			return _byType.TryGetValue(type, out var list) ? list : null;
		}

		/// <summary>
		/// Two-step weighted selection: pick a <see cref="ShopItemType"/> from config weights,
		/// then pick an item of that type from per-item weights.
		/// Both rolls should be in [0, 1).
		/// </summary>
		public IPurchasable PickItem(float typeRoll, float itemRoll)
		{
			ShopConfig.ShopItemTypeWeight picked = WeightedSelection.Pick(_config.itemTypeWeights, typeRoll);

			if (!_byType.TryGetValue(picked.type, out var candidates) || candidates.Count == 0)
				return null;

			return WeightedSelection.Pick(candidates, itemRoll);
		}
	}
}
