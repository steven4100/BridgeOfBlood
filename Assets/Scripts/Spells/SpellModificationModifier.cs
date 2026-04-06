using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	public enum SpellModificationProperty : byte
	{
		CritChance = 0,
		CritMult = 1,
		Chains = 2,
		Pierce = 3,
		AreaOfEffect = 4,
		Duration = 5,
		CastSpeed = 6,
		Projectiles = 7,

		DamageScaling = 8,

		PhysicalDamageScaling = 10,
		ColdDamageScaling = 11,
		FireDamageScaling = 12,
		LightningDamageScaling = 13,

		PhysicalPenetration = 20,
		ColdPenetration = 21,
		FirePenetration = 22,
		LightningPenetration = 23,
	}

	[Serializable]
	public class ParameterModifier
	{
		public SpellModificationProperty property;
		public SpellAttributeMask filter;

		[SerializeReference, SerializeInterface]
		public IValue<float> flatAdditive;

		[SerializeReference, SerializeInterface]
		public IValue<float> percentIncreased;

		[SerializeReference, SerializeInterface]
		public IValue<float> moreMultiplier;

		public float GetFlat() => flatAdditive?.Resolve(null) ?? 0f;
		public float GetPercent() => percentIncreased?.Resolve(null) ?? 0f;
		public float GetMore() => moreMultiplier?.Resolve(null) ?? 0f;

		public ParameterModifier Clone()
		{
			return new ParameterModifier
			{
				property = property,
				filter = filter,
				flatAdditive = flatAdditive,
				percentIncreased = percentIncreased,
				moreMultiplier = moreMultiplier,
			};
		}
	}
}
