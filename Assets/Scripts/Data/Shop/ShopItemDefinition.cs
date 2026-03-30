using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Shop
{
	/// <summary>
	/// Shop listing metadata embedded on <see cref="IPurchasable"/> assets (items, spells).
	/// </summary>
	[Serializable]
	public sealed class ShopItemDefinition : IRandomElement
	{
		[Header("Display")]
		[SerializeField] string displayName;
		[SerializeField, TextArea] string description;

		[Header("Economy")]
		[SerializeField] int price;
		[SerializeField] int resellValue;
		[SerializeField] bool isResellable;

		[Header("Classification")]
		[SerializeField] Rarity rarity;
		[SerializeField] ShopItemType shopItemType;
		[SerializeField] float weight = 1f;

		public string DisplayName => displayName;
		public string Description => description;
		public int Price => price;
		public int ResellValue => resellValue;
		public bool IsResellable => isResellable;
		public Rarity Rarity => rarity;
		public ShopItemType ShopItemType => shopItemType;

		float IRandomElement.Weight
		{
			get => weight;
			set => weight = value;
		}
	}
}
