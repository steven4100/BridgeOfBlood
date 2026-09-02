using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;

namespace BridgeOfBlood.Data.Spells
{
	public sealed class RuntimeSpellGemCollection
	{
		readonly RuntimeSpell _host;
		readonly RuntimeGem[] _slots;
		readonly Action _changed;

		public int SlotCount => _slots.Length;

		public RuntimeSpell Host => _host;

		public RuntimeSpellGemCollection(RuntimeSpell host, int slotCount, Action changed)
		{
			_host = host;
			_slots = new RuntimeGem[slotCount];
			_changed = changed;
		}

		public RuntimeGem GetSlot(int index)
		{
			return _slots[index];
		}

		public int FilledCount
		{
			get
			{
				int n = 0;
				for (int i = 0; i < _slots.Length; i++)
				{
					if (_slots[i] != null)
						n++;
				}
				return n;
			}
		}

		public int FirstEmptySlot
		{
			get
			{
				for (int i = 0; i < _slots.Length; i++)
				{
					if (_slots[i] == null)
						return i;
				}
				return -1;
			}
		}

		public int IndexOf(RuntimeGem gem)
		{
			for (int i = 0; i < _slots.Length; i++)
			{
				if (ReferenceEquals(_slots[i], gem))
					return i;
			}
			return -1;
		}

		public List<RuntimeGem> ExtractAll()
		{
			var extracted = new List<RuntimeGem>(_slots.Length);
			bool any = false;
			for (int i = 0; i < _slots.Length; i++)
			{
				if (_slots[i] == null)
					continue;
				extracted.Add(_slots[i]);
				_slots[i] = null;
				any = true;
			}
			if (any)
				_changed?.Invoke();
			return extracted;
		}

		public bool CanPlace(RuntimeGem gem, int index, RuntimeGem relocating)
		{
			if (gem == null || !gem.Definition.CanApplyToSpell(_host))
				return false;
			if (index < 0 || index >= _slots.Length)
				return false;
			RuntimeGem existing = _slots[index];
			if (existing != null
				&& !ReferenceEquals(existing, gem)
				&& !ReferenceEquals(existing, relocating))
				return false;
			return true;
		}

		public bool TryInsert(RuntimeGem gem, int index)
		{
			if (!CanPlace(gem, index, null))
				return false;
			_slots[index] = gem;
			_changed?.Invoke();
			return true;
		}

		public bool TryRemove(RuntimeGem gem)
		{
			for (int i = 0; i < _slots.Length; i++)
			{
				if (!ReferenceEquals(_slots[i], gem))
					continue;
				_slots[i] = null;
				_changed?.Invoke();
				return true;
			}
			return false;
		}

		public bool OwnsPayload(UnityEngine.ScriptableObject asset)
		{
			for (int i = 0; i < _slots.Length; i++)
			{
				if (_slots[i] != null && ReferenceEquals(_slots[i].Definition, asset))
					return true;
			}
			return false;
		}
	}
}
