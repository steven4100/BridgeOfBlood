using System.Collections.Generic;
using UnityEngine;

namespace BridgeOfBlood.Data.Enemies
{
	[System.Serializable]
	public class EnemySpawnEntry
	{
		public EnemyAuthoringData enemy;
		[Min(0f)]
		public float weight = 1f;
		[Tooltip("Spawn patterns for this enemy type; one is chosen at random per pick. If empty, table fallback is used.")]
		public List<SpawnPattern> spawnPatterns = new List<SpawnPattern>();
		[Tooltip("Scale applied to pattern positions relative to spawn origin. 1 = as designed, >1 = spread out, <1 = cluster in.")]
		[Min(0.01f)]
		public float positionScale = 1f;
	}

	/// <summary>Result of a weighted spawn pick: which enemy, which pattern, and scale for positions.</summary>
	public struct EnemySpawnPick
	{
		public EnemyAuthoringData enemy;
		public SpawnPattern pattern;
		public float positionScale;
	}

	/// <summary>
	/// ScriptableObject that defines which enemies can spawn and their relative weights.
	/// At runtime, PickEnemyByWeight() returns one enemy type per spawn event.
	/// </summary>
	[CreateAssetMenu(fileName = "EnemySpawnTable", menuName = "Bridge of Blood/Enemies/Enemy Spawn Table", order = 0)]
	public class EnemySpawnTable : ScriptableObject
	{
		[Tooltip("Used when an entry has no spawn pattern assigned.")]
		public SpawnPattern fallbackSpawnPattern;
		public List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();

		static SpawnPattern PickRandomSpawnPattern(List<SpawnPattern> patterns)
		{
			if (patterns.Count == 0) return null;
			return patterns[Random.Range(0, patterns.Count)];
		}

		static SpawnPattern PickRandomSpawnPattern(List<SpawnPattern> patterns, ref Unity.Mathematics.Random rng)
		{
			if (patterns.Count == 0) return null;
			return patterns[rng.NextInt(0, patterns.Count)];
		}

		/// <summary>
		/// Picks one enemy and its spawn pattern by weighted random. Pattern may be null (use fallback).
		/// </summary>
		public EnemySpawnPick PickEnemyByWeight()
		{
			float total = 0f;
			foreach (var e in entries)
			{
				if (e.weight > 0f)
					total += e.weight;
			}
			if (total <= 0f) return default;

			float roll = Random.Range(0f, total);
			foreach (var e in entries)
			{
				if (e.weight <= 0f) continue;
				roll -= e.weight;
				if (roll <= 0f)
					return new EnemySpawnPick { enemy = e.enemy, pattern = PickRandomSpawnPattern(e.spawnPatterns), positionScale = e.positionScale };
			}
			for (int i = entries.Count - 1; i >= 0; i--)
			{
				var e = entries[i];
				if (e.weight > 0f)
					return new EnemySpawnPick { enemy = e.enemy, pattern = PickRandomSpawnPattern(e.spawnPatterns), positionScale = e.positionScale };
			}
			return default;
		}

		/// <summary>
		/// Deterministic pick using a seed. Returns enemy and its tethered pattern (or null for fallback).
		/// </summary>
		public EnemySpawnPick PickEnemyByWeight(uint seed)
		{
			float total = 0f;
			foreach (var e in entries)
			{
				if (e.weight > 0f)
					total += e.weight;
			}
			if (total <= 0f) return default;

			var rng = Unity.Mathematics.Random.CreateFromIndex(seed);
			float roll = rng.NextFloat() * total;
			foreach (var e in entries)
			{
				if (e.weight <= 0f) continue;
				roll -= e.weight;
				if (roll <= 0f)
					return new EnemySpawnPick { enemy = e.enemy, pattern = PickRandomSpawnPattern(e.spawnPatterns, ref rng), positionScale = e.positionScale };
			}
			for (int i = entries.Count - 1; i >= 0; i--)
			{
				var e = entries[i];
				if (e.weight > 0f)
					return new EnemySpawnPick { enemy = e.enemy, pattern = PickRandomSpawnPattern(e.spawnPatterns, ref rng), positionScale = e.positionScale };
			}
			return default;
		}
	}
}
