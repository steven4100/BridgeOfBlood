using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>
	/// Authoring data for enemies. Written into SoA slots during spawn.
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
		public DamageType elementalWeakness;

		[Header("Visual")]
		[Tooltip("Sprite visual for atlas-based rendering. Run Tools > BridgeOfBlood > Rebuild Sprite Rendering Data after assigning.")]
		public SpriteProvider visual;

		[Header("Audio")]
		[Tooltip("Optional audio unit emitted when this enemy dies.")]
		public AudioUnit onDeathSound;

		/// <summary>
		/// Writes one enemy into an existing parallel-column slot. Uses deterministic random seed for moveSpeed.
		/// </summary>
		public void WriteRuntimeColumnsAt(
			int index,
			float2 position,
			uint randomSeed,
			NativeList<EnemyMotion> motion,
			NativeList<EnemyVitality> vitality,
			NativeList<EnemyCombatTraits> combatTraits,
			NativeList<StatusAilmentFlag> status,
			NativeList<EnemyPresentation> presentation)
		{
			var random = Unity.Mathematics.Random.CreateFromIndex(randomSeed);
			float moveSpeed = random.NextFloat(minMoveSpeed, maxMoveSpeed);

			motion[index] = new EnemyMotion { position = position, moveSpeed = moveSpeed, knockbackVelocity = float2.zero };
			vitality[index] = new EnemyVitality { health = healthPoints, maxHealth = healthPoints };
			combatTraits[index] = new EnemyCombatTraits
			{
				corruptionFlag = corruptionFlag,
				elementalWeakness = elementalWeakness
			};
			status[index] = 0;
			presentation[index] = new EnemyPresentation
			{
				visual = visual != null ? visual.Resolve(randomSeed) : EntityVisual.None,
				onDeathSound = onDeathSound != null ? onDeathSound.ToRuntime(randomSeed ^ 0x2C7E31A1u) : AudioUnitRuntime.None,
				visualTime = 0f,
				ailmentFlashTimer = 0f,
				ailmentFlashSource = default
			};
		}
	}
}

