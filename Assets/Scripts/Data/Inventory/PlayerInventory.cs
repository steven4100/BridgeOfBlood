using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// Authoring + runtime inventory. Template lists define the starting layout; call <see cref="RebuildFromStartingDefinition"/>
	/// after <see cref="Object.Instantiate(UnityEngine.Object)"/> for a new session. During play, rows live in <see cref="inventoryItems"/>.
	/// </summary>
	[CreateAssetMenu(fileName = "PlayerInventory", menuName = "Bridge of Blood/Inventory/Player Inventory")]
	public sealed class PlayerInventory : ScriptableObject, IItemInventoryService
	{
		public int startingNumberOfSpells = 32;
		public List<SpellAuthoringData> startingSpells = new List<SpellAuthoringData>();
		public List<Item> startingItems = new List<Item>();

		
		[SerializeField]
		List<InventoryItem> inventoryItems = new List<InventoryItem>();

		List<Item> _passiveItemScratch = new List<Item>();
		readonly List<InventoryItem> _passiveItemRowScratch = new List<InventoryItem>();

		bool _suppressItemsUpdated;
		Action _itemsUpdated;

		SpellCollection _spellCollection = new SpellCollection(null);

		public SpellCollection SpellCollection => _spellCollection;

		public IReadOnlyList<InventoryItem> StoredRows => inventoryItems;

		IReadOnlyList<InventoryItem> IItemInventoryService.GetPassiveItemRows()
		{
			_passiveItemRowScratch.Clear();
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (inventoryItems[i].Payload is Item)
					_passiveItemRowScratch.Add(inventoryItems[i]);
			}
			return _passiveItemRowScratch;
		}

		bool IItemInventoryService.TrySetPassiveItemOrder(IReadOnlyList<InventoryItem> reorderedItemRows)
		{
			_passiveItemRowScratch.Clear();
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (inventoryItems[i].Payload is Item)
					_passiveItemRowScratch.Add(inventoryItems[i]);
			}

			if (reorderedItemRows.Count != _passiveItemRowScratch.Count)
			{
				_passiveItemRowScratch.Clear();
				return false;
			}
			if (_passiveItemRowScratch.Count == 0)
			{
				_passiveItemRowScratch.Clear();
				return reorderedItemRows.Count == 0;
			}

			for (int i = 0; i < reorderedItemRows.Count; i++)
			{
				InventoryItem row = reorderedItemRows[i];
				if (!_passiveItemRowScratch.Contains(row))
				{
					_passiveItemRowScratch.Clear();
					return false;
				}
				for (int j = i + 1; j < reorderedItemRows.Count; j++)
				{
					if (ReferenceEquals(reorderedItemRows[i], reorderedItemRows[j]))
					{
						_passiveItemRowScratch.Clear();
						return false;
					}
				}
			}

			_passiveItemRowScratch.Clear();

			int o = 0;
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (inventoryItems[i].Payload is Item)
					inventoryItems[i] = reorderedItemRows[o++];
			}

			NotifyItemsUpdated();
			return true;
		}

		event Action IItemInventoryService.ItemsUpdated
		{
			add => _itemsUpdated += value;
			remove => _itemsUpdated -= value;
		}

		void NotifyItemsUpdated()
		{
			if (_suppressItemsUpdated) return;
			_itemsUpdated?.Invoke();
		}

		public void Clear()
		{
			inventoryItems.Clear();
			NotifyItemsUpdated();
		}

		public void Add(InventoryItem row)
		{
			if (row == null || row.Payload == null) return;
			inventoryItems.Add(row);
			NotifyItemsUpdated();
		}

		public void AddSpell(SpellAuthoringData spell){
			_spellCollection.AddSpell(spell);
			inventoryItems.Add(new InventoryItem(spell));
			NotifyItemsUpdated();
		}

		public void AddItem(Item item)
		{
			_passiveItemScratch.Add(item);
			inventoryItems.Add(new InventoryItem(item));
			NotifyItemsUpdated();
		}

		public IEnumerable<T> GetAllFromInventory<T>() where T : class, IInventoryItem
		{
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (inventoryItems[i].Payload is T match)
					yield return match;
			}
		}

		/// <summary>
		/// True if a row already uses this asset as its payload (reference equality).
		/// </summary>
		public bool OwnsPayload(IInventoryItem asset)
		{
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (ReferenceEquals(inventoryItems[i].Payload, asset))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Clears runtime rows and repopulates from <see cref="startingSpells"/> / <see cref="startingItems"/> using <see cref="startingNumberOfSpells"/>.
		/// </summary>
		public void RebuildFromStartingDefinition()
		{
			_suppressItemsUpdated = true;
			try
			{
				inventoryItems.Clear();

				int cap = Mathf.Max(0, startingNumberOfSpells);
				int addedSpells = 0;
				if (startingSpells != null)
				{
					for (int i = 0; i < startingSpells.Count && addedSpells < cap; i++)
					{
						SpellAuthoringData spell = startingSpells[i];
						if (spell == null) continue;
						addedSpells++;
						Add(new InventoryItem(spell));
						AddSpell(spell);
					}
				}

				if (startingItems != null)
				{
					for (int i = 0; i < startingItems.Count; i++)
					{
						Item item = startingItems[i];
						if (item == null) continue;
						Add(new InventoryItem(item));
						AddItem(item);
					}
				}
			}
			finally
			{
				_suppressItemsUpdated = false;
			}
			NotifyItemsUpdated();
		}

		

		/// <summary>
		/// Valid until the next call that mutates inventory or calls this again.
		/// </summary>
		public IReadOnlyList<Item> GetPassiveItems()
		{
			_passiveItemScratch.Clear();
			for (int i = 0; i < inventoryItems.Count; i++)
			{
				if (inventoryItems[i].Payload is Item item && item != null)
					_passiveItemScratch.Add(item);
			}
			return _passiveItemScratch;
		}
	}
}
