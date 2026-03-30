using System;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Blood quota for round index n (1-based): baseQuota * multiplierPerRound^(n-1) + additivePerRound * (n-1).
	/// </summary>
	[Serializable]
	public struct BloodQuotaScaling
	{
		[Tooltip("Blood required to pass round 1.")]
		public float baseQuota;

		[Tooltip("Per-round multiplier k in base * k^(n-1). Use 1 for flat quota each round.")]
		public float multiplierPerRound;

		[Tooltip("Added linearly: B * (n-1) after the exponential term.")]
		public float additivePerRound;

		public RoundRuntimeData BuildForRound(int roundNumber)
		{
			int n = Mathf.Max(1, roundNumber);
			float expTerm = baseQuota * Mathf.Pow(multiplierPerRound, n - 1);
			float linearTerm = additivePerRound * (n - 1);
			return new RoundRuntimeData { bloodRequirement = expTerm + linearTerm };
		}
	}

	/// <summary>
	/// Resolved values for one round (extend with more fields later).
	/// </summary>
	[Serializable]
	public struct RoundRuntimeData
	{
		public float bloodRequirement;
	}

	[CreateAssetMenu(fileName = "GameConfig", menuName = "Bridge of Blood/Game Config")]
	public class GameConfig : ScriptableObject
	{
		[Header("Round")]
		public BloodQuotaScaling bloodQuotaScaling = new BloodQuotaScaling
		{
			baseQuota = 1000f,
			multiplierPerRound = 1f,
			additivePerRound = 0f
		};

		[Tooltip("Complete spell loops allowed per round.")]
		public int maxSpellLoopsPerRound = 3;

		[Header("Session defaults")]
		[Tooltip("Template wallet (starting gold). Instantiate at session start — do not use the template reference at runtime.")]
		public PlayerWallet playerWallet;

		[Tooltip("Template inventory (starting spells/items). Instantiate and call RebuildFromStartingDefinition at session start.")]
		public PlayerInventory playerInventory;

		[Header("Shop")]
		[Tooltip("Weighted shop type/item rules. Shared authoring asset — not cloned with the runtime GameConfig.")]
		public ShopConfig shopConfig;

		/// <summary>
		/// Builds a session-owned <see cref="GameConfig"/> clone: duplicates this asset, then unique wallet/inventory instances
		/// so runtime mutation never touches the authoring asset on disk.
		/// </summary>
		public static GameConfig CreateRuntimeCopy(GameConfig template)
		{
			GameConfig copy = Instantiate(template);
			copy.playerWallet = Instantiate(template.playerWallet);
			copy.playerInventory = Instantiate(template.playerInventory);
			copy.playerInventory.RebuildFromStartingDefinition();
			return copy;
		}

		/// <summary>
		/// Destroys a runtime copy from <see cref="CreateRuntimeCopy"/> (wallet, inventory, then config).
		/// </summary>
		public static void DestroyRuntimeCopy(GameConfig runtime)
		{
			if (runtime == null) return;
			PlayerWallet w = runtime.playerWallet;
			PlayerInventory inv = runtime.playerInventory;
			runtime.playerWallet = null;
			runtime.playerInventory = null;
			if (w != null) UnityEngine.Object.Destroy(w);
			if (inv != null) UnityEngine.Object.Destroy(inv);
			UnityEngine.Object.Destroy(runtime);
		}
	}
}
