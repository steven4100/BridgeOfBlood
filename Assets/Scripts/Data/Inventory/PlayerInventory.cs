using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// Authoring + runtime inventory. Template lists define the starting layout; call <see cref="RebuildFromStartingDefinition"/>
	/// after <see cref="Object.Instantiate(UnityEngine.Object)"/> for a new session. During play, rows live in <see cref="_rows"/>.
	/// </summary>
	[CreateAssetMenu(fileName = "PlayerInventory", menuName = "Bridge of Blood/Inventory/Player Inventory")]
	public sealed class PlayerInventory : ScriptableObject
	{
		[Header("Starting layout (template)")]
		[Tooltip("Maximum starter spells taken from Starting Spells (first N non-null).")]
		public int startingNumberOfSpells = 32;

		public List<SpellAuthoringData> startingSpells = new List<SpellAuthoringData>();
		public List<Item> startingItems = new List<Item>();

		[SerializeField]
		List<InventoryItem> _rows = new List<InventoryItem>();

		readonly List<SpellAuthoringData> _spellLoopScratch = new List<SpellAuthoringData>();
		readonly List<Item> _passiveItemScratch = new List<Item>();

		public IReadOnlyList<InventoryItem> StoredRows => _rows;

		public void Clear()
		{
			_rows.Clear();
		}

		public void Add(InventoryItem row)
		{
			if (row == null || row.Payload == null) return;
			_rows.Add(row);
		}

		public void AddPayload(IInventoryItem payload, int stackCount = 1, int resellValue = 0, bool isResellable = false)
		{
			if (payload == null) return;
			_rows.Add(new InventoryItem(payload, stackCount, resellValue, isResellable));
		}

		public IEnumerable<T> GetAllFromInventory<T>() where T : class, IInventoryItem
		{
			for (int i = 0; i < _rows.Count; i++)
			{
				if (_rows[i].Payload is T match)
					yield return match;
			}
		}

		/// <summary>
		/// True if a row already uses this asset as its payload (reference equality).
		/// </summary>
		public bool OwnsPayload(IInventoryItem asset)
		{
			for (int i = 0; i < _rows.Count; i++)
			{
				if (ReferenceEquals(_rows[i].Payload, asset))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Clears runtime rows and repopulates from <see cref="startingSpells"/> / <see cref="startingItems"/> using <see cref="startingNumberOfSpells"/>.
		/// </summary>
		public void RebuildFromStartingDefinition()
		{
			Clear();

			int cap = Mathf.Max(0, startingNumberOfSpells);
			int addedSpells = 0;
			if (startingSpells != null)
			{
				for (int i = 0; i < startingSpells.Count && addedSpells < cap; i++)
				{
					SpellAuthoringData spell = startingSpells[i];
					if (spell == null) continue;
					AddPayload(spell);
					addedSpells++;
				}
			}

			if (startingItems == null) return;
			for (int i = 0; i < startingItems.Count; i++)
			{
				Item item = startingItems[i];
				if (item != null)
					AddPayload(item);
			}
		}

		/// <summary>
		/// Valid until the next call that mutates inventory or calls this again.
		/// </summary>
		public IReadOnlyList<SpellAuthoringData> GetSpellLoopAuthoring()
		{
			_spellLoopScratch.Clear();
			for (int i = 0; i < _rows.Count; i++)
			{
				if (_rows[i].Payload is SpellAuthoringData spell && spell != null)
					_spellLoopScratch.Add(spell);
			}
			return _spellLoopScratch;
		}

		/// <summary>
		/// Valid until the next call that mutates inventory or calls this again.
		/// </summary>
		public IReadOnlyList<Item> GetPassiveItems()
		{
			_passiveItemScratch.Clear();
			for (int i = 0; i < _rows.Count; i++)
			{
				if (_rows[i].Payload is Item item && item != null)
					_passiveItemScratch.Add(item);
			}
			return _passiveItemScratch;
		}
	}
}
