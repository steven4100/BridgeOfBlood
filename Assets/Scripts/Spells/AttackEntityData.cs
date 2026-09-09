using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "AttackEntityData", menuName = "BridgeOfBlood/Spells/Attack Entity Data")]
public class AttackEntityData : ScriptableObject, ISerializationCallbackReceiver
{
	public Vector2 entityVelocity;
	public float rehitCooldownSeconds;

	[Tooltip("Optional behaviors (damage, crit, hit box, pierce, expiration, chain). Only present behaviors are serialized.")]
	[SerializeReference]
	[SerializeInterface]
	public List<AttackEntityBehavior> behaviors = new List<AttackEntityBehavior>
	{
		new OnHitEffectBehavior(),
		new OnKillEffectBehavior()
	};

	[Header("Visual")]
	[Tooltip("Sprite visual for atlas-based rendering. Run Tools > BridgeOfBlood > Rebuild Sprite Rendering Data after assigning.")]
	public SpriteProvider visual;

	[Header("Audio")]
	[Tooltip("Optional audio unit emitted when this attack deals damage.")]
	public AudioUnit onDamageSound;
	

	public bool HasBehavior<T>() where T : AttackEntityBehavior => GetBehavior<T>() != null;

	public T GetBehavior<T>() where T : AttackEntityBehavior
	{
		if (behaviors == null)
			return null;
		for (int i = 0; i < behaviors.Count; i++)
		{
			if (behaviors[i] is T match)
				return match;
		}
		return null;
	}

	public IReadOnlyList<AttackEntityBehavior> GetAllBehaviors()
	{
		return behaviors;
	}

	public void OnBeforeSerialize() { }

	public void OnAfterDeserialize()
	{
		behaviors ??= new List<AttackEntityBehavior>();
	}

	void OnValidate()
	{
		behaviors ??= new List<AttackEntityBehavior>();
	}

	void TryAddDamage<T>(FloatRange range, float scalar) where T : DamageBehavior, new()
	{
		if (HasBehavior<T>())
			return;
		bool hasRange = range.min > 0f || range.max > 0f;
		bool hasScalar = scalar > 0f;
		if (!hasRange && !hasScalar)
			return;
		var behavior = new T
		{
			damageRange = hasRange ? range : new FloatRange { min = scalar, max = scalar }
		};
		behaviors.Add(behavior);
	}
}
