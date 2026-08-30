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

	[SerializeField, HideInInspector, FormerlySerializedAs("physicalDamageRange")]
	FloatRange legacyPhysicalDamageRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("coldDamageRange")]
	FloatRange legacyColdDamageRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("fireDamageRange")]
	FloatRange legacyFireDamageRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("lightningDamageRange")]
	FloatRange legacyLightningDamageRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("critChanceRange")]
	FloatRange legacyCritChanceRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("critDamageMultiplierRange")]
	FloatRange legacyCritDamageMultiplierRange;
	[SerializeField, HideInInspector, FormerlySerializedAs("knockbackStrength")]
	float legacyKnockbackStrength;
	[SerializeField, HideInInspector, FormerlySerializedAs("hitBoxData")]
	HitBoxData legacyHitBoxData;
	[SerializeField, HideInInspector, FormerlySerializedAs("physicalDamage")]
	float legacyPhysicalDamage;
	[SerializeField, HideInInspector, FormerlySerializedAs("coldDamage")]
	float legacyColdDamage;
	[SerializeField, HideInInspector, FormerlySerializedAs("fireDamage")]
	float legacyFireDamage;
	[SerializeField, HideInInspector, FormerlySerializedAs("lightningDamage")]
	float legacyLightningDamage;
	[SerializeField, HideInInspector, FormerlySerializedAs("critChance")]
	float legacyCritChance;
	[SerializeField, HideInInspector, FormerlySerializedAs("critDamageMultiplier")]
	float legacyCritDamageMultiplier;

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

	public void OnBeforeSerialize() { }

	public void OnAfterDeserialize()
	{
		behaviors ??= new List<AttackEntityBehavior>();
		MigrateLegacyFields();
	}

	void OnValidate()
	{
		behaviors ??= new List<AttackEntityBehavior>();
		MigrateLegacyFields();
	}

	void MigrateLegacyFields()
	{
		TryAddDamage<PhysicalDamageBehavior>(legacyPhysicalDamageRange, legacyPhysicalDamage);
		TryAddDamage<ColdDamageBehavior>(legacyColdDamageRange, legacyColdDamage);
		TryAddDamage<FireDamageBehavior>(legacyFireDamageRange, legacyFireDamage);
		TryAddDamage<LightningDamageBehavior>(legacyLightningDamageRange, legacyLightningDamage);

		bool hasCritRange = legacyCritChanceRange.min > 0f || legacyCritChanceRange.max > 0f
			|| legacyCritDamageMultiplierRange.min > 1f || legacyCritDamageMultiplierRange.max > 1f;
		bool hasCritScalar = legacyCritChance > 0f
			|| (legacyCritDamageMultiplier > 1f);
		if (!HasBehavior<CritBehavior>() && (hasCritRange || hasCritScalar))
		{
			FloatRange chance = hasCritRange ? legacyCritChanceRange : new FloatRange { min = legacyCritChance, max = legacyCritChance };
			FloatRange mult = hasCritRange ? legacyCritDamageMultiplierRange : new FloatRange { min = legacyCritDamageMultiplier, max = legacyCritDamageMultiplier };
			if (mult.min <= 0f && mult.max <= 0f)
				mult = new FloatRange { min = 1f, max = 1f };
			behaviors.Add(new CritBehavior { critChanceRange = chance, critDamageMultiplierRange = mult });
		}

		if (!HasBehavior<KnockbackBehavior>() && legacyKnockbackStrength > 0f)
			behaviors.Add(new KnockbackBehavior { knockbackStrength = legacyKnockbackStrength });

		bool hasHitBox = legacyHitBoxData.isSphere || legacyHitBoxData.isRect
			|| legacyHitBoxData.sphereRadius > 0f
			|| legacyHitBoxData.rectDimension.x > 0f || legacyHitBoxData.rectDimension.y > 0f;
		if (!HasBehavior<HitBoxBehavior>() && hasHitBox)
			behaviors.Add(new HitBoxBehavior { hitBoxData = legacyHitBoxData });
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
