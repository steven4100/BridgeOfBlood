using System;
using System.Collections.Generic;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	public sealed class ItemCollection
	{
		readonly List<RuntimeItem> _items = new List<RuntimeItem>();
		readonly List<RuntimeItem> _orderScratch = new List<RuntimeItem>();
		readonly List<Item> _passiveScratch = new List<Item>();

		public IReadOnlyList<RuntimeItem> Items => _items;
		public int Count => _items.Count;

		public event Action ItemsUpdated;

		public IReadOnlyList<Item> GetPassiveItems()
		{
			_passiveScratch.Clear();
			for (int i = 0; i < _items.Count; i++)
				_passiveScratch.Add(_items[i].Definition);
			return _passiveScratch;
		}

		public int IndexOf(RuntimeItem item)
		{
			for (int i = 0; i < _items.Count; i++)
			{
				if (ReferenceEquals(_items[i], item))
					return i;
			}
			return -1;
		}

		public bool TryInsert(RuntimeItem item, int index)
		{
			if (item == null)
				return false;
			if (IndexOf(item) >= 0)
				return false;

			if (index < 0 || index > _items.Count)
				index = _items.Count;
			_items.Insert(index, item);
			ItemsUpdated?.Invoke();
			return true;
		}

		public bool TrySetOrder(IReadOnlyList<RuntimeItem> reordered)
		{
			if (reordered.Count != _items.Count)
				return false;

			_orderScratch.Clear();
			for (int i = 0; i < reordered.Count; i++)
			{
				RuntimeItem row = reordered[i];
				if (!_items.Contains(row))
				{
					_orderScratch.Clear();
					return false;
				}
				for (int j = i + 1; j < reordered.Count; j++)
				{
					if (ReferenceEquals(reordered[i], reordered[j]))
					{
						_orderScratch.Clear();
						return false;
					}
				}
				_orderScratch.Add(row);
			}

			_items.Clear();
			_items.AddRange(_orderScratch);
			_orderScratch.Clear();
			ItemsUpdated?.Invoke();
			return true;
		}

		public void Clear()
		{
			_items.Clear();
			ItemsUpdated?.Invoke();
		}

		public bool OwnsPayload(ScriptableObject asset)
		{
			for (int i = 0; i < _items.Count; i++)
			{
				if (ReferenceEquals(_items[i].Definition, asset))
					return true;
			}
			return false;
		}

		public bool TryRemove(RuntimeItem item)
		{
			int index = IndexOf(item);
			if (index < 0)
				return false;
			_items.RemoveAt(index);
			ItemsUpdated?.Invoke();
			return true;
		}
	}
}
