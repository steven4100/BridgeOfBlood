using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>
	/// Authoring data for enemies. Converted to runtime Enemy struct during initialization.
	/// Simple design: HP, movement pattern, speed, elemental weakness.
	/// </summary>
	[CreateAssetMenu(fileName = "EnemyData", menuName = "BridgeOfBlood/Enemies/Enemy Authoring Data")]
	public class EnemyAuthoringData : ScriptableObject
	{
		[Header("Movement")]
		[Tooltip("Minimum movement speed")]
		public float minMoveSpeed = 1f;

		[Tooltip("Maximum movement speed")]
		public float maxMoveSpeed = 2f;

		[Header("Combat")]
		[Tooltip("Base health points")]
		public float healthPoints = 100f;

		[Header("Characteristics")]
		[Tooltip("Corruption type(s) for conditional modifiers")]
		public EnemyCorruptionFlag corruptionFlag;

		[Tooltip("Elemental weakness(es) - takes increased damage from these types")]
		public SpellDamageType elementalWeakness;

		/// <summary>
		/// Creates a runtime Enemy struct from this authoring data.
		/// Uses deterministic random seed for moveSpeed calculation.
		/// </summary>
		public Enemy CreateRuntimeEnemy(float2 position, int entityId, uint randomSeed)
		{
			var random = Unity.Mathematics.Random.CreateFromIndex(randomSeed);
			var moveSpeed = random.NextFloat(minMoveSpeed, maxMoveSpeed);

			return new Enemy
			{
				position = position,
				moveSpeed = moveSpeed,
				health = healthPoints,
				maxHealth = healthPoints,
				corruptionFlag = corruptionFlag,
				elementalWeakness = elementalWeakness,
				statusAilmentFlag = 0,
				entityId = entityId
			};
		}
	}
}

