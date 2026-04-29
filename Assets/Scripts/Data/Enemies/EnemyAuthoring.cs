using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>
	/// Authoring data for enemies. Appended as SoA column rows during spawn.
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
		/// Appends one enemy as parallel column rows. Uses deterministic random seed for moveSpeed.
		/// </summary>
		public void AppendRuntimeColumns(
			float2 position,
			int entityId,
			uint randomSeed,
			NativeList<EnemyMotion> motion,
			NativeList<EnemyVitality> vitality,
			NativeList<int> entityIds,
			NativeList<EnemyCombatTraits> combatTraits,
			NativeList<StatusAilmentFlag> status,
			NativeList<EnemyPresentation> presentation)
		{
			var random = Unity.Mathematics.Random.CreateFromIndex(randomSeed);
			float moveSpeed = random.NextFloat(minMoveSpeed, maxMoveSpeed);

			motion.Add(new EnemyMotion { position = position, moveSpeed = moveSpeed, knockbackVelocity = float2.zero });
			vitality.Add(new EnemyVitality { health = healthPoints, maxHealth = healthPoints });
			entityIds.Add(entityId);
			combatTraits.Add(new EnemyCombatTraits
			{
				corruptionFlag = corruptionFlag,
				elementalWeakness = elementalWeakness
			});
			status.Add(0);
			presentation.Add(new EnemyPresentation
			{
				visual = visual != null ? visual.Resolve(randomSeed) : EntityVisual.None,
				onDeathSound = onDeathSound != null ? onDeathSound.ToRuntime(randomSeed ^ 0x2C7E31A1u) : AudioUnitRuntime.None,
				visualTime = 0f,
				ailmentFlashTimer = 0f,
				ailmentFlashSource = default
			});
		}
	}
}

