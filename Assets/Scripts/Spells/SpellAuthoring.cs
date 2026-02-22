using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	[CreateAssetMenu(fileName = "SpellData", menuName = "BridgeOfBlood/Spells/Spell Authoring Data")]
	public class SpellAuthoringData : ScriptableObject
	{
		public SpellAnimation SpellAnimation;
		public int baseMultiplier = 1;
		public float castCompletionDuration = 1f;
		public float castTime = 0.5f;
		public SpellAttributeMask attributeMask;
	}

	public class SpellAnimation
	{
		public List<SpellKeyFrame> keyFrames = new List<SpellKeyFrame>();
	}

	public class SpellKeyFrame
	{
        public float time;
		public List<AttackEntityData> entitiesToSpawn = new List<AttackEntityData>();
	}
	
}

public struct Damage
{
    public DamageType type;
    public int baseDamage;
}

public struct AttackEntityData
{
    public FixedList32Bytes<Damage> damages;
    public Vector2 entityVelocity;
    public AttackEntityLifecycleData lifecycleData;
    public HitBoxData hitBoxData;


    public AttackEntitySpawnType spawnType;
    public RelativeToPlayerSpawnCriteria relativeToPlayerSpawnCriteria;
    public NearestEnemySpawnCriteria nearestEnemySpawnCriteria;
}

public struct HitBoxData
{
    public bool isSphere;
    public bool isRect;
    public Vector2 rectDimension;
    public float sphereRadius;
    public float scaleGrowthRate;
}

public struct AttackEntityLifecycleData
{
    public int maxNumEnemiesHit;
    public float maxTimeAlive;
    public float maxDistanceTravelled;
}

public enum AttackEntitySpawnType
{
    RelativeToPlayer,
    NearestEnemy
}

public struct RelativeToPlayerSpawnCriteria
{
    public Vector2 offsetFromPlayer;
}

public struct NearestEnemySpawnCriteria
{
    public float minDistanceFromPlayer;
    public float maxDistanceFromPlayer;
}