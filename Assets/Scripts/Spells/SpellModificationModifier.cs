using System;

namespace BridgeOfBlood.Data.Spells
{
	public enum ModifierOperation : byte
	{
		Add = 0,
		Subtract = 1
	}

	public enum SpellModificationProperty : byte
	{
		CritChanceFlatAdd,
		CritChancePercentInc,
		CritMultFlatAdd,
		CritMultPercentInc,
		ChainsFlatAdd,
		ChainsPercentInc,
		PierceFlatAdd,
		PiercePercentInc,
		AoEFlatAdd,
		AoEPercentInc,
		DurationFlatAdd,
		DurationPercentInc,
		CastSpeedFlatAdd,
		CastSpeedPercentInc,
		ProjectilesFlatAdd,
		ProjectilesPercentInc,
	}

	[Serializable]
	public struct SpellModificationModifier
	{
		public SpellModificationProperty property;
		public ModifierOperation operation;
		public float value;
	}

	public static class SpellModificationResolver
	{
		public static void Apply(in SpellModificationModifier modifier, SpellModifications target)
		{
			int delta = modifier.operation == ModifierOperation.Subtract
				? -(int)modifier.value
				: (int)modifier.value;

			switch (modifier.property)
			{
				case SpellModificationProperty.CritChanceFlatAdd:
					target.criticalStrikeChance = EnsureAndApplyFlat(target.criticalStrikeChance, delta);
					break;
				case SpellModificationProperty.CritChancePercentInc:
					target.criticalStrikeChance = EnsureAndApplyPercent(target.criticalStrikeChance, delta);
					break;
				case SpellModificationProperty.CritMultFlatAdd:
					target.criticalStrikeMultiplier = EnsureAndApplyFlat(target.criticalStrikeMultiplier, delta);
					break;
				case SpellModificationProperty.CritMultPercentInc:
					target.criticalStrikeMultiplier = EnsureAndApplyPercent(target.criticalStrikeMultiplier, delta);
					break;
				case SpellModificationProperty.ChainsFlatAdd:
					target.chains = EnsureAndApplyFlat(target.chains, delta);
					break;
				case SpellModificationProperty.ChainsPercentInc:
					target.chains = EnsureAndApplyPercent(target.chains, delta);
					break;
				case SpellModificationProperty.PierceFlatAdd:
					target.pierce = EnsureAndApplyFlat(target.pierce, delta);
					break;
				case SpellModificationProperty.PiercePercentInc:
					target.pierce = EnsureAndApplyPercent(target.pierce, delta);
					break;
				case SpellModificationProperty.AoEFlatAdd:
					target.areaOfEffect = EnsureAndApplyFlat(target.areaOfEffect, delta);
					break;
				case SpellModificationProperty.AoEPercentInc:
					target.areaOfEffect = EnsureAndApplyPercent(target.areaOfEffect, delta);
					break;
				case SpellModificationProperty.DurationFlatAdd:
					target.duration = EnsureAndApplyFlat(target.duration, delta);
					break;
				case SpellModificationProperty.DurationPercentInc:
					target.duration = EnsureAndApplyPercent(target.duration, delta);
					break;
				case SpellModificationProperty.CastSpeedFlatAdd:
					target.castSpeed = EnsureAndApplyFlat(target.castSpeed, delta);
					break;
				case SpellModificationProperty.CastSpeedPercentInc:
					target.castSpeed = EnsureAndApplyPercent(target.castSpeed, delta);
					break;
				case SpellModificationProperty.ProjectilesFlatAdd:
					target.numberOfProjectiles = EnsureAndApplyFlat(target.numberOfProjectiles, delta);
					break;
				case SpellModificationProperty.ProjectilesPercentInc:
					target.numberOfProjectiles = EnsureAndApplyPercent(target.numberOfProjectiles, delta);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(modifier.property), modifier.property, null);
			}
		}

		static ParamaterModifier EnsureAndApplyFlat(ParamaterModifier param, int delta)
		{
			param ??= new ParamaterModifier();
			param.flatAdditiveValue += delta;
			return param;
		}

		static ParamaterModifier EnsureAndApplyPercent(ParamaterModifier param, int delta)
		{
			param ??= new ParamaterModifier();
			param.percentIncreased += delta;
			return param;
		}
	}
}
