using System;
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

		/// <summary>
		/// Returns a runtime clone of this spell with the given modifications baked in (damage, chains, pierce, area).
		/// Keyframe entity data is cloned and modified values are applied so the returned spell is ready to cast as-is.
		/// </summary>
		public SpellAuthoringData Modify(SpellModifications modifications)
		{
			if (modifications == null)
				return this;

			var clone = CreateInstance<SpellAuthoringData>();
			clone.baseMultiplier = baseMultiplier;
			clone.castCompletionDuration = castCompletionDuration;
			clone.castTime = castTime;
			clone.attributeMask = attributeMask;

			if (SpellAnimation?.keyFrames != null && SpellAnimation.keyFrames.Count > 0)
			{
				clone.SpellAnimation = new SpellAnimation();
				foreach (var kf in SpellAnimation.keyFrames)
				{
					if (kf == null) continue;
					var entityData = kf.attackEntityData != null
						? SpellModificationsApplicator.CloneAndApply(kf.attackEntityData, attributeMask, modifications)
						: kf.attackEntityData;
					clone.SpellAnimation.keyFrames.Add(new SpellKeyFrame
					{
						time = kf.time,
						attackEntityEmitter = kf.attackEntityEmitter,
						attackEntityData = entityData
					});
				}
			}
			else
			{
				clone.SpellAnimation = SpellAnimation;
			}

			return clone;
		}
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
        [Tooltip("Emission pattern (spread, forward). Returns spawn position + direction per emit.")]
        public AttackEntityEmitter attackEntityEmitter;
        [Tooltip("Entity to spawn. Handler builds payload from this and game modifiers.")]
        public AttackEntityData attackEntityData;
	}
	
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
public struct RelativeToPlayerSpawnCriteria
{
    public Vector2 offsetFromPlayer;
}

/// <summary>
/// Attribute for custom drawer: List of AttackEntityBehavior with Add Pierce/Expiration/Chain.
/// </summary>
public class AttackEntityBehaviorsListAttribute : PropertyAttribute { }