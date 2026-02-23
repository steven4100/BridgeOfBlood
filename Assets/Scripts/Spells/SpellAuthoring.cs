using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
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

    [System.Serializable]
	public class SpellAnimation
	{
		public List<SpellKeyFrame> keyFrames = new List<SpellKeyFrame>();
	}
    [System.Serializable]
    public class SpellKeyFrame
	{
        public float time;
		public List<AttackEntityData> entitiesToSpawn = new List<AttackEntityData>();
	}
	
}

[System.Serializable]
public struct Damage
{
    public DamageType type;
    public int baseDamage;
}

[System.Serializable]
public struct AttackEntityData
{
    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public Vector2 entityVelocity;
    public AttackEntityLifecycleData lifecycleData;
    public HitBoxData hitBoxData;


    public AttackEntitySpawnType spawnType;
    public RelativeToPlayerSpawnCriteria relativeToPlayerSpawnCriteria;
    public NearestEnemySpawnCriteria nearestEnemySpawnCriteria;
}

[System.Serializable]
public struct HitBoxData
{
    public bool isSphere;
    public bool isRect;
    public Vector2 rectDimension;
    public float sphereRadius;
    public float scaleGrowthRate;
}

[System.Serializable]
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

[System.Serializable]
public struct RelativeToPlayerSpawnCriteria
{
    public Vector2 offsetFromPlayer;
}

[System.Serializable]
public struct NearestEnemySpawnCriteria
{
    public float minDistanceFromPlayer;
    public float maxDistanceFromPlayer;
}