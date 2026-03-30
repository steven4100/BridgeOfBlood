using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Shop
{
	[CreateAssetMenu(fileName = "ShopConfig", menuName = "Bridge of Blood/Shop/Shop Config")]
	public class ShopConfig : ScriptableObject
	{
		[Serializable]
		public class ShopItemTypeWeight : IRandomElement
		{
			public ShopItemType type;
			[SerializeField] float weight = 1f;

			float IRandomElement.Weight
			{
				get => weight;
				set => weight = value;
			}
		}

		public List<ShopItemTypeWeight> itemTypeWeights;
	}
}
