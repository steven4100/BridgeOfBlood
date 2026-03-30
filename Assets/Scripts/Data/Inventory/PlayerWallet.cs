using UnityEngine;

namespace BridgeOfBlood.Data.Inventory
{
	/// <summary>
	/// Authoring + runtime wallet. Assign a template asset on <see cref="BridgeOfBlood.Data.Shared.GameConfig"/>;
	/// at session start <see cref="Object.Instantiate(UnityEngine.Object)"/> so <see cref="TrySpend"/> does not mutate the shared template.
	/// </summary>
	[CreateAssetMenu(fileName = "PlayerWallet", menuName = "Bridge of Blood/Inventory/Player Wallet")]
	public class PlayerWallet : ScriptableObject
	{
		[Tooltip("Starting gold when this asset is instantiated for a session.")]
		public int gold;

		public bool TrySpend(int amount)
		{
			if (amount <= 0)
				return true;
			if (gold < amount)
				return false;
			gold -= amount;
			return true;
		}
	}
}
