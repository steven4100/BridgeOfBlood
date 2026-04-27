using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "AttackEntityData", menuName = "BridgeOfBlood/Spells/Attack Entity Data")]
public class AttackEntityData : ScriptableObject
{
	[Header("Damage (rolled once per spell keyframe)")]
	public FloatRange physicalDamageRange;
	public FloatRange coldDamageRange;
	public FloatRange fireDamageRange;
	public FloatRange lightningDamageRange;

	[Header("Crit (rolled once per keyframe; hit still rolls success vs crit chance)")]
	public FloatRange critChanceRange;
	public FloatRange critDamageMultiplierRange;

	

	public Vector2 entityVelocity;
	public HitBoxData hitBoxData;
	public float rehitCooldownSeconds;

	[Tooltip("Optional behaviors (pierce, expiration, chain). Only present behaviors are serialized.")]
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

	public void OnBeforeSerialize() { }

	public void OnAfterDeserialize()
	{
		SanitizeRanges();
	}

	void OnValidate()
	{
		SanitizeRanges();
	}

	void SanitizeRanges()
	{
		physicalDamageRange.ClampOrder();
		coldDamageRange.ClampOrder();
		fireDamageRange.ClampOrder();
		lightningDamageRange.ClampOrder();
		critChanceRange.ClampOrder();
		critDamageMultiplierRange.ClampOrder();

		physicalDamageRange.min = Mathf.Max(0f, physicalDamageRange.min);
		physicalDamageRange.max = Mathf.Max(0f, physicalDamageRange.max);
		coldDamageRange.min = Mathf.Max(0f, coldDamageRange.min);
		coldDamageRange.max = Mathf.Max(0f, coldDamageRange.max);
		fireDamageRange.min = Mathf.Max(0f, fireDamageRange.min);
		fireDamageRange.max = Mathf.Max(0f, fireDamageRange.max);
		lightningDamageRange.min = Mathf.Max(0f, lightningDamageRange.min);
		lightningDamageRange.max = Mathf.Max(0f, lightningDamageRange.max);

		critChanceRange.min = Mathf.Clamp01(critChanceRange.min);
		critChanceRange.max = Mathf.Clamp01(critChanceRange.max);

		if (critDamageMultiplierRange.min <= 0f && critDamageMultiplierRange.max <= 0f)
			critDamageMultiplierRange = new FloatRange { min = 1f, max = 1f };
		else
		{
			critDamageMultiplierRange.min = Mathf.Max(1f, critDamageMultiplierRange.min);
			critDamageMultiplierRange.max = Mathf.Max(1f, critDamageMultiplierRange.max);
		}
	}
}
