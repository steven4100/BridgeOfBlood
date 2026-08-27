using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Per-round spawn and objective tuning. Round index is 1-based via <see cref="GameConfig.GetRoundConfig"/>.
	/// </summary>
	[Serializable]
	public class RoundConfig
	{
		[SerializeReference, SerializeInterface]
		[Tooltip("Spawn timing/origins for this round. Pick Enemy Spawner from the type menu.")]
		public IEnemySpawner spawner;

		[Range(0f, 100f)]
		[Tooltip("Percent of enemies spawned this round that must be killed to go to shop instead of lose.")]
		public float killQuotaPercent;

		[Tooltip("Multiplier on authored enemy min/max move speed.")]
		public float enemyMoveSpeedMultiplier;

		[Tooltip("Multiplier on authored enemy health.")]
		public float enemyHealthMultiplier;

		[Tooltip("Playfield width and height in simulation space. Origin is x = 0 (left) .. width (right); y = ±height/2.")]
		public Vector2 simulationSize = new Vector2(1600f, 800f);

		public int ResolveMinEnemiesKilled(int enemiesSpawnedThisRound)
		{
			float t = Mathf.Clamp(killQuotaPercent, 0f, 100f) * 0.01f;
			return Mathf.CeilToInt(enemiesSpawnedThisRound * t);
		}

		/// <summary>
		/// Playfield in simulation space: x = 0 (left) .. width (right); y = 0 at vertical center (±height/2).
		/// </summary>
		public Rect ResolvePlayfield()
		{
			float w = simulationSize.x;
			float h = simulationSize.y;
			return new Rect(0f, -h * 0.5f, w, h);
		}
	}

	[CreateAssetMenu(fileName = "GameConfig", menuName = "Bridge of Blood/Game Config")]
	public class GameConfig : ScriptableObject
	{
		public SimulationConfig simulationConfig = new SimulationConfig();

		[Header("Round")]
		[Tooltip("Per-round spawners, kill quota, simulation size, and enemy scaling. Round n uses index n-1; later rounds reuse the last entry.")]
		public List<RoundConfig> roundConfigs = new List<RoundConfig>
		{
			new RoundConfig
			{
				killQuotaPercent = 100f,
				enemyMoveSpeedMultiplier = 1f,
				enemyHealthMultiplier = 1f,
				simulationSize = new Vector2(1600f, 800f)
			}
		};

		[Tooltip("Complete spell loops allowed per round.")]
		public int maxSpellLoopsPerRound = 3;

		[Tooltip("Mana budget for one spell loop. Prefix cost of the next spell above this resets the loop.")]
		public float totalMana = 100f;

		[Tooltip("Player WASD move speed at session start (simulation units per second).")]
		public float playerStartMoveSpeed = 100f;

		[Tooltip("Optional lab/debug mods merged into every frame's global spell modifications.")]
		public SpellModificationsTestData castModifications;

		[Header("Session defaults")]
		[Tooltip("Template wallet (starting gold). Instantiate at session start — do not use the template reference at runtime.")]
		public PlayerWallet playerWallet;

		[Tooltip("Template inventory (starting spells/items). Instantiate and call RebuildFromStartingDefinition at session start.")]
		public PlayerInventory playerInventory;

		[Header("Shop")]
		[Tooltip("Weighted shop type/item rules. Shared authoring asset — not cloned with the runtime GameConfig.")]
		public ShopConfig shopConfig;

		/// <summary>
		/// 1-based round lookup. Rounds past the list reuse the last authored config.
		/// </summary>
		public RoundConfig GetRoundConfig(int roundNumber)
		{
			int n = Mathf.Max(1, roundNumber);
			int i = Mathf.Min(n - 1, roundConfigs.Count - 1);
			return roundConfigs[i];
		}

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
