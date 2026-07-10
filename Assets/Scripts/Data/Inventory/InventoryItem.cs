using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// One row in the player inventory (owned payload + stack).
	/// Payload is a <see cref="ScriptableObject"/> asset reference (<see cref="Item"/> or <see cref="BridgeOfBlood.Data.Spells.SpellAuthoringData"/>).
	/// </summary>
	[Serializable]
	public sealed class InventoryItem
	{
		[SerializeField]
		ScriptableObject _payload;

		[SerializeField, FormerlySerializedAs("stackCount")]
		int _stackCount;

		[SerializeField, FormerlySerializedAs("resellValue")]
		int _resellValue;

		[SerializeField, FormerlySerializedAs("isResellable")]
		bool _isResellable;

		public ScriptableObject Payload => _payload;

		public int StackCount => _stackCount < 1 ? 1 : _stackCount;
		public int ResellValue => _resellValue < 0 ? 0 : _resellValue;
		public bool IsResellable => _isResellable;

		public InventoryItem(ScriptableObject payload, int stackCount = 1, int resellValue = 0, bool isResellable = false)
		{
			_payload = payload;
			_stackCount = stackCount < 1 ? 1 : stackCount;
			_resellValue = resellValue < 0 ? 0 : resellValue;
			_isResellable = isResellable;
		}

		/// <summary>For Unity deserialization only.</summary>
		public InventoryItem() { }
	}
}
