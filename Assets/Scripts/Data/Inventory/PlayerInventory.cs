using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// Authoring + runtime inventory. Template lists define the starting layout; call <see cref="RebuildFromStartingDefinition"/>
	/// after <see cref="Object.Instantiate(UnityEngine.Object)"/> for a new session.
	/// </summary>
	[CreateAssetMenu(fileName = "PlayerInventory", menuName = "Bridge of Blood/Inventory/Player Inventory")]
	public sealed class PlayerInventory : ScriptableObject, IInventoryService
	{
		public int startingNumberOfSpells = 32;
		public List<SpellAuthoringData> startingSpells = new List<SpellAuthoringData>();
		public List<Item> startingItems = new List<Item>();

		[Tooltip("Assets placed into the stash at session start. Supports SpellAuthoringData, SpellItem (gems), and Item (jokers).")]
		public List<ScriptableObject> startingStash = new List<ScriptableObject>();

		[SerializeField] int stashWidth = 8;
		[SerializeField] int stashHeight = 4;

		bool _suppressItemsUpdated;

		SpellCollection _spellCollection = new SpellCollection(null);
		ItemCollection _itemCollection = new ItemCollection();
		Stash _stash;

		public SpellCollection SpellCollection => _spellCollection;
		public ItemCollection Items => _itemCollection;
		public Stash Stash => _stash ??= new Stash(stashWidth, stashHeight);

		void OnEnable()
		{
			_itemCollection.ItemsUpdated -= OnItemCollectionUpdated;
			_itemCollection.ItemsUpdated += OnItemCollectionUpdated;
		}

		void OnDisable()
		{
			_itemCollection.ItemsUpdated -= OnItemCollectionUpdated;
		}

		void OnItemCollectionUpdated()
		{
			if (_suppressItemsUpdated) return;
			_itemsUpdated?.Invoke();
		}

		Action _itemsUpdated;

		event Action IInventoryService.ItemsUpdated
		{
			add => _itemsUpdated += value;
			remove => _itemsUpdated -= value;
		}

		public void AddItem(Item item)
		{
			if (item is Gem)
				return;
			_itemCollection.TryInsert(new RuntimeItem(item), _itemCollection.Count);
		}

		IReadOnlyList<RuntimeItem> IInventoryService.GetItems() => _itemCollection.Items;

		bool IInventoryService.TrySetItemOrder(IReadOnlyList<RuntimeItem> reordered)
		{
			return _itemCollection.TrySetOrder(reordered);
		}

		public void Clear()
		{
			_itemCollection.Clear();
			_spellCollection.ClearSpells();
			Stash.Clear();
		}

		public void AddSpell(SpellAuthoringData spell)
		{
			_spellCollection.AddSpell(spell);
		}

		/// <summary>
		/// True if a runtime occupant already uses this asset (reference equality).
		/// </summary>
		public bool OwnsPayload(ScriptableObject asset)
		{
			if (_spellCollection.OwnsPayload(asset))
				return true;
			if (_itemCollection.OwnsPayload(asset))
				return true;
			if (Stash.OwnsPayload(asset))
				return true;
			return false;
		}

		/// <summary>
		/// Clears runtime occupants and repopulates from <see cref="startingSpells"/> / <see cref="startingItems"/> /
		/// <see cref="startingStash"/> using <see cref="startingNumberOfSpells"/>.
		/// </summary>
		public void RebuildFromStartingDefinition()
		{
			_suppressItemsUpdated = true;
			try
			{
				_itemCollection.Clear();
				_spellCollection.ClearSpells();
				Stash.Clear();

				int cap = Mathf.Max(0, startingNumberOfSpells);
				int addedSpells = 0;
				if (startingSpells != null)
				{
					for (int i = 0; i < startingSpells.Count && addedSpells < cap; i++)
					{
						SpellAuthoringData spell = startingSpells[i];
						if (spell == null) continue;
						addedSpells++;
						AddSpell(spell);
					}
				}

				if (startingItems != null)
				{
					for (int i = 0; i < startingItems.Count; i++)
					{
						Item item = startingItems[i];
						if (item == null) continue;
						if (item is Gem spellItem)
						{
							TryPlaceInStash(new RuntimeGem(spellItem));
							continue;
						}
						AddItem(item);
					}
				}

				if (startingStash != null)
				{
					for (int i = 0; i < startingStash.Count; i++)
					{
						ScriptableObject asset = startingStash[i];
						if (asset == null) continue;
						TryAddStartingStashAsset(asset);
					}
				}
			}
			finally
			{
				_suppressItemsUpdated = false;
			}
			_itemsUpdated?.Invoke();
		}

		void TryAddStartingStashAsset(ScriptableObject asset)
		{
			switch (asset)
			{
				case SpellAuthoringData spell:
					TryPlaceInStash(new RuntimeSpell(spell));
					break;
				case Gem gem:
					TryPlaceInStash(new RuntimeGem(gem));
					break;
				case Item item:
					TryPlaceInStash(new RuntimeItem(item));
					break;
				default:
					Debug.LogWarning(
						$"[PlayerInventory] startingStash entry '{asset.name}' is not a spell, gem, or item; skipped.",
						this);
					break;
			}
		}

		bool TryPlaceInStash(IInventoryOccupant occupant)
		{
			if (!Stash.TryFindFirstFreeCell(out Vector2Int cell))
				return false;
			return Stash.TryPlace(occupant, cell);
		}

		/// <summary>
		/// Valid until the next call that mutates inventory or calls this again.
		/// </summary>
		public IReadOnlyList<Item> GetPassiveItems()
		{
			return _itemCollection.GetPassiveItems();
		}
	}
}
