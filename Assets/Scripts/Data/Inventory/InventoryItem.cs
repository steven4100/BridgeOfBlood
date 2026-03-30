using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// One row in the player inventory (owned payload + stack).
	/// Payload is serialized via Unity <c>SerializeReference</c> (not a <see cref="UnityEngine.Object"/> field).
	/// For save games, persist stable asset ids + stack counts separately from Unity references.
	/// </summary>
	[Serializable]
	public sealed class InventoryItem
	{
		[SerializeReference, SerializeField]
		IInventoryItem _payload;

		[SerializeField, FormerlySerializedAs("stackCount")]
		int _stackCount;

		[SerializeField, FormerlySerializedAs("resellValue")]
		int _resellValue;

		[SerializeField, FormerlySerializedAs("isResellable")]
		bool _isResellable;

		public IInventoryItem Payload => _payload;

		public int StackCount => _stackCount < 1 ? 1 : _stackCount;
		public int ResellValue => _resellValue < 0 ? 0 : _resellValue;
		public bool IsResellable => _isResellable;

		public InventoryItem(IInventoryItem payload, int stackCount = 1, int resellValue = 0, bool isResellable = false)
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
