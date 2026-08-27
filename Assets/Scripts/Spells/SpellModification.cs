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

        KnockbackStrength = 24,

        ManaCost = 25,
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

    [System.Serializable]
    public class SpellModifications
    {
        public Dictionary<SpellModificationProperty, List<ParameterModifier>> modifiers = new Dictionary<SpellModificationProperty, List<ParameterModifier>>();
        public List<DamageConversion> conversions;
        public List<ExtraDamageAs> extraDamageAs;
        public List<AttackEntityModifier> attackEntityModifiers = new List<AttackEntityModifier>();

        public bool IsEmpty =>
            modifiers.Count == 0
            && (conversions == null || conversions.Count == 0)
            && (extraDamageAs == null || extraDamageAs.Count == 0)
            && attackEntityModifiers.Count == 0;

        public void Clear()
        {
            modifiers.Clear();
            conversions?.Clear();
            extraDamageAs?.Clear();
            attackEntityModifiers.Clear();
        }

        public void MergeFrom(SpellModifications other)
        {
            if (other == null)
                return;

            foreach (var kvp in other.modifiers)
            {
                if (!modifiers.TryGetValue(kvp.Key, out List<ParameterModifier> list))
                {
                    list = new List<ParameterModifier>();
                    modifiers[kvp.Key] = list;
                }
                list.AddRange(kvp.Value);
            }

            if (other.conversions != null && other.conversions.Count > 0)
            {
                conversions ??= new List<DamageConversion>();
                conversions.AddRange(other.conversions);
            }

            if (other.extraDamageAs != null && other.extraDamageAs.Count > 0)
            {
                extraDamageAs ??= new List<ExtraDamageAs>();
                extraDamageAs.AddRange(other.extraDamageAs);
            }

            if (other.attackEntityModifiers.Count > 0)
                attackEntityModifiers.AddRange(other.attackEntityModifiers);
        }

        public void Add(ParameterModifier modifier)
        {
            if (!modifiers.TryGetValue(modifier.property, out var list))
            {
                list = new List<ParameterModifier>();
                modifiers[modifier.property] = list;
            }
            list.Add(modifier);
        }

        public void Add(AttackEntityModifier modifier)
        {
            attackEntityModifiers.Add(modifier);
        }

        public SpellModifications Clone()
        {
            var copy = new SpellModifications();
            copy.MergeFrom(this);
            return copy;
        }
    }

    /// <summary>
    /// Frame spell modifications: a global bucket plus per-<see cref="RuntimeSpell.spellId"/> overrides.
    /// Call <see cref="FinalizeResolution"/> after item evaluation, then read via <see cref="ResolveFor"/>.
    /// </summary>
    public class SpellModificationCollection
    {
        public SpellModifications global = new SpellModifications();

        readonly Dictionary<int, SpellModifications> perSpellId = new Dictionary<int, SpellModifications>();
        readonly Dictionary<int, SpellModifications> resolvedBySpellId = new Dictionary<int, SpellModifications>();
        readonly List<SpellModifications> mergedAllocations = new List<SpellModifications>();

        public void ResetForFrame()
        {
            global.Clear();
            perSpellId.Clear();
            resolvedBySpellId.Clear();
            mergedAllocations.Clear();
        }

        public SpellModifications ForSpell(int spellId)
        {
            if (!perSpellId.TryGetValue(spellId, out SpellModifications mods))
            {
                mods = new SpellModifications();
                perSpellId[spellId] = mods;
            }
            return mods;
        }

        /// <summary>
        /// Merges global + per-spell buckets into stable <see cref="SpellModifications"/> instances for each loop slot.
        /// </summary>
        public void FinalizeResolution(IReadOnlyList<RuntimeSpell> spells)
        {
            resolvedBySpellId.Clear();
            mergedAllocations.Clear();

            if (spells == null)
                return;

            for (int i = 0; i < spells.Count; i++)
                resolvedBySpellId[spells[i].spellId] = BuildMerged(spells[i].spellId);
        }

        /// <summary>
        /// Resolved modifications for <paramref name="spellId"/> after <see cref="FinalizeResolution"/>.
        /// </summary>
        public SpellModifications ResolveFor(int spellId)
        {
            if (resolvedBySpellId.TryGetValue(spellId, out SpellModifications resolved))
                return resolved;
            return global;
        }

        SpellModifications BuildMerged(int spellId)
        {
            perSpellId.TryGetValue(spellId, out SpellModifications specific);
            bool hasGlobal = global != null && !global.IsEmpty;
            bool hasSpecific = specific != null && !specific.IsEmpty;

            if (hasGlobal && hasSpecific)
            {
                var merged = new SpellModifications();
                merged.MergeFrom(global);
                merged.MergeFrom(specific);
                mergedAllocations.Add(merged);
                return merged;
            }

            if (hasSpecific)
                return specific;
            return global;
        }
    }
}
