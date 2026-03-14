using BridgeOfBlood.Data.Shared;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
    public struct DamageConversion
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 50 = 50%
    }
    public struct FlatDamage
    {
        public DamageType type;
        public float min;
        public float max;
    }
    [System.Serializable]
    public class ParamaterModifier
    {
        public int flatAdditiveValue;
        // Additive bucket
        public int percentIncreased;

        // Each entry is a multiplicative modifier (e.g. 1.2f for 20% more)
        public List<float> moreMultipliers;

        public void Add(ParamaterModifier other)
        {
            if (other == null)
                return;

            flatAdditiveValue += other.flatAdditiveValue;
            percentIncreased += other.percentIncreased;

            if (other.moreMultipliers != null)
            {
                moreMultipliers ??= new List<float>();
                moreMultipliers.AddRange(other.moreMultipliers);
            }
        }

        public ParamaterModifier Clone()
        {
            return new ParamaterModifier
            {
                flatAdditiveValue = flatAdditiveValue,
                percentIncreased = percentIncreased,
                moreMultipliers = moreMultipliers != null ? new List<float>(moreMultipliers) : null
            };
        }
    }

    public struct ExtraDamageAs
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 20 = 20%
    }

    [System.Serializable]
    public class SpellModifications
    {
        // Core parameters
        public ParamaterModifier criticalStrikeChance;
        public ParamaterModifier criticalStrikeMultiplier;
        public ParamaterModifier chains;
        public ParamaterModifier pierce;
        public ParamaterModifier areaOfEffect;
        public ParamaterModifier duration;
        public ParamaterModifier castSpeed;
        public ParamaterModifier numberOfProjectiles;

        // Damage scaling
        public Dictionary<SpellAttributeMask, ParamaterModifier> spellAttributeDamageScaling;
        public Dictionary<DamageType, ParamaterModifier> damageTypeScaling;
        public Dictionary<DamageType, ParamaterModifier> damageTypePenetration;

        // Flat added damage
        public List<FlatDamage> flatAddedDamage;

        // Conversion
        public List<DamageConversion> conversions;

        // Extra damage as
        public List<ExtraDamageAs> extraDamageAs;

        public static SpellModifications Combine(List<SpellModifications> mods)
        {
            if (mods == null || mods.Count == 0)
                return new SpellModifications();

            SpellModifications combined = new SpellModifications();

            foreach (var mod in mods)
            {
                combined.Merge(mod);
            }

            return combined;
        }

        public void Merge(SpellModifications other)
        {
            if (other == null)
                return;

            MergeParam(ref criticalStrikeChance, other.criticalStrikeChance);
            MergeParam(ref criticalStrikeMultiplier, other.criticalStrikeMultiplier);
            MergeParam(ref chains, other.chains);
            MergeParam(ref pierce, other.pierce);
            MergeParam(ref areaOfEffect, other.areaOfEffect);
            MergeParam(ref duration, other.duration);
            MergeParam(ref castSpeed, other.castSpeed);
            MergeParam(ref numberOfProjectiles, other.numberOfProjectiles);

            MergeDictionary(other.spellAttributeDamageScaling, ref spellAttributeDamageScaling);
            MergeDictionary(other.damageTypeScaling, ref damageTypeScaling);
            MergeDictionary(other.damageTypePenetration, ref damageTypePenetration);

            MergeList(other.flatAddedDamage, ref flatAddedDamage);
            MergeList(other.conversions, ref conversions);
            MergeList(other.extraDamageAs, ref extraDamageAs);
        }

        private static void MergeParam(ref ParamaterModifier target, ParamaterModifier source)
        {
            if (source == null)
                return;

            target ??= new ParamaterModifier();
            target.Add(source);
        }

        private static void MergeDictionary<T>(
            Dictionary<T, ParamaterModifier> source,
            ref Dictionary<T, ParamaterModifier> target)
        {
            if (source == null)
                return;

            target ??= new Dictionary<T, ParamaterModifier>();

            foreach (var kvp in source)
            {
                if (!target.TryGetValue(kvp.Key, out var existing))
                {
                    existing = new ParamaterModifier();
                    target[kvp.Key] = existing;
                }

                existing.Add(kvp.Value);
            }
        }

        private static void MergeList<T>(List<T> source, ref List<T> target)
        {
            if (source == null)
                return;

            target ??= new List<T>();
            target.AddRange(source);
        }
    }



}

