using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
    public struct DamageConversion
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 50 = 50%
    }

    public struct ExtraDamageAs
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 20 = 20%
    }
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

    public class SpellModificationCollection
    {
        public SpellModifications globalModifications = new SpellModifications();
        public Dictionary<RuntimeSpell, SpellModifications> spellSpecificModifications = new Dictionary<RuntimeSpell, SpellModifications>();
        public SpellModificationCollection() { }

    }

    [System.Serializable]
    public class SpellModifications
    {
        public Dictionary<SpellModificationProperty, List<ParameterModifier>> modifiers = new Dictionary<SpellModificationProperty, List<ParameterModifier>>();
        public List<DamageConversion> conversions;
        public List<ExtraDamageAs> extraDamageAs;

        public void Add(ParameterModifier modifier)
        {
            if (!modifiers.TryGetValue(modifier.property, out var list))
            {
                list = new List<ParameterModifier>();
                modifiers[modifier.property] = list;
            }
            list.Add(modifier);
        }

        public static SpellModifications Combine(List<SpellModifications> mods)
        {
            if (mods == null || mods.Count == 0)
                return new SpellModifications();

            SpellModifications combined = new SpellModifications();
            foreach (var mod in mods)
                combined.Merge(mod);
            return combined;
        }

        public void Merge(SpellModifications other)
        {
            if (other == null)
                return;

            foreach (var kvp in other.modifiers)
            {
                if (!modifiers.TryGetValue(kvp.Key, out var list))
                {
                    list = new List<ParameterModifier>();
                    modifiers[kvp.Key] = list;
                }
                foreach (var m in kvp.Value)
                    list.Add(m.Clone());
            }

            MergeList(other.conversions, ref conversions);
            MergeList(other.extraDamageAs, ref extraDamageAs);
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
