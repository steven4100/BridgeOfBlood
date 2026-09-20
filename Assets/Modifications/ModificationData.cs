using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using BridgeOfBlood.Telemetry;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
    public interface IModificationData
    {
        IModificationData Bake(EffectContext context);
        string GetDescription();
        void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination);
    }

    /// <summary>
    /// Reusable numeric stacking payload. It only contains flat, increased, and more;
    /// consumers decide how a merged result changes their authored value.
    /// </summary>
    [Serializable]
    public sealed class ParameterModification
    {
        [SerializeReference, SerializeInterface]
        public IValue<float> flatAdditive;

        [SerializeReference, SerializeInterface]
        public IValue<float> percentIncreased;

        [SerializeReference, SerializeInterface]
        public IValue<float> moreMultiplier;

        public float GetFlat() => flatAdditive?.Resolve(null) ?? 0f;
        public float GetPercent() => percentIncreased?.Resolve(null) ?? 0f;
        public float GetMore() => moreMultiplier?.Resolve(null) ?? 0f;

        public ParameterModification Bake(EffectContext context)
        {
            var baked = new ParameterModification();
            if (flatAdditive != null)
                baked.flatAdditive = new ConstantValue { value = flatAdditive.Resolve(context) };
            if (percentIncreased != null)
                baked.percentIncreased = new ConstantValue { value = percentIncreased.Resolve(context) };
            if (moreMultiplier != null)
                baked.moreMultiplier = new ConstantValue { value = moreMultiplier.Resolve(context) };
            return baked;
        }

        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination)
        {
            flatAdditive?.CollectTelemetrySubscriptions(destination);
            percentIncreased?.CollectTelemetrySubscriptions(destination);
            moreMultiplier?.CollectTelemetrySubscriptions(destination);
        }
    }

    [Serializable]
    public sealed class ChainModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new ChainModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "chains", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class PierceModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new PierceModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "pierce", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class AreaOfEffectModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new AreaOfEffectModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "area of effect", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class KnockbackStrengthModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new KnockbackStrengthModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "knockback", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class CritChanceModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new CritChanceModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "crit chance", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class CritMultiplierModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new CritMultiplierModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "crit multiplier", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class DamageScalingModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new DamageScalingModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "damage", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class PhysicalDamageScalingModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new PhysicalDamageScalingModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "physical damage", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class ColdDamageScalingModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new ColdDamageScalingModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "cold damage", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class FireDamageScalingModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new FireDamageScalingModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "fire damage", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class LightningDamageScalingModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new LightningDamageScalingModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "lightning damage", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class ProjectileModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new ProjectileModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "projectiles", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class MulticastModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new MulticastModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "multicast", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class ManaCostModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new ManaCostModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "mana cost", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class DurationModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new DurationModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "duration", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class CastSpeedModificationData : IModificationData
    {
        public SpellAttributeMask filter;
        public ParameterModification parameters = new ParameterModification();
        public IModificationData Bake(EffectContext context) => new CastSpeedModificationData { filter = filter, parameters = parameters.Bake(context) };
        public string GetDescription() => EffectDescription.Describe(parameters, "cast speed", filter);
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) => parameters.CollectTelemetrySubscriptions(destination);
    }

    [Serializable]
    public sealed class DamageConversionModificationData : IModificationData
    {
        public DamageConversion conversion;
        public IModificationData Bake(EffectContext context) => new DamageConversionModificationData { conversion = conversion };
        public string GetDescription() => $"{conversion.percent:0.##}% of {conversion.from} damage converted to {conversion.to}";
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) { }
    }

    [Serializable]
    public sealed class ExtraDamageAsModificationData : IModificationData
    {
        public ExtraDamageAs extraDamageAs;
        public IModificationData Bake(EffectContext context) => new ExtraDamageAsModificationData { extraDamageAs = extraDamageAs };
        public string GetDescription() => $"Gain {extraDamageAs.percent:0.##}% of {extraDamageAs.from} damage as extra {extraDamageAs.to}";
        public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) { }
    }

    /// <summary>
    /// Aggregates parameter values only. It deliberately does not apply the result to damage,
    /// counts, durations, or any other domain value.
    /// </summary>
    public static class ParameterModificationMerger
    {
        public static ResolvedModifier Merge<T>(
            ModificationCollection<T> modifications,
            Func<T, ParameterModification> getParameters,
            Func<T, SpellAttributeMask> getFilter,
            SpellAttributeMask mask)
            where T : IModificationData
        {
            float flat = 0f;
            float increased = 0f;
            float more = 1f;

            for (int i = 0; i < modifications.Count; i++)
            {
                T channel = modifications[i];
                SpellAttributeMask filter = getFilter(channel);
                if (filter != SpellAttributeMask.None && (mask & filter) == 0)
                    continue;

                ParameterModification parameter = getParameters(channel);
                flat += parameter.GetFlat();
                increased += parameter.GetPercent();

                float nextMore = parameter.GetMore();
                if (nextMore != 0f)
                    more *= 1f + nextMore / 100f;
            }

            return new ResolvedModifier
            {
                flat = flat,
                percentIncreased = increased,
                moreCombined = more
            };
        }
    }
}
