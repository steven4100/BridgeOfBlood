#nullable enable

using BridgeOfBlood.Data.Shared;
using System;
using System.Collections.Generic;
using Unity.Collections;

namespace BridgeOfBlood.Telemetry
{
    // =========================================================================
    // CMS-FACING TYPES
    // =========================================================================

    /// <summary>
    /// Base type for all telemetry stat configurations.
    ///
    /// The CMS serializer is expected to serialize these polymorphically and
    /// preserve the concrete runtime type.
    /// </summary>
    [Serializable]
    public abstract class TelemetryStatArgs
    {
        /// <summary>
        /// Stable identity for this configured query. Not GetHashCode().
        /// </summary>
        public abstract string GetKey();

        /// <summary>Player-facing phrase without time-scope wording.</summary>
        public abstract string GetDescription();

        /// <summary>
        /// False when the aggregator already materializes this query from other stats
        /// (e.g. DPS from damage / duration).
        /// </summary>
        public virtual bool RequiresEventSubscription => true;

        public static string AilmentKeySuffix(StatusAilmentFlag flags)
        {
            if (flags == StatusAilmentFlag.None)
                return string.Empty;

            string suffix = string.Empty;
            if ((flags & StatusAilmentFlag.Frozen) != 0) suffix += ":Frozen";
            if ((flags & StatusAilmentFlag.Stunned) != 0) suffix += ":Stunned";
            if ((flags & StatusAilmentFlag.Poisoned) != 0) suffix += ":Poisoned";
            if ((flags & StatusAilmentFlag.Ignited) != 0) suffix += ":Ignited";
            if ((flags & StatusAilmentFlag.Shocked) != 0) suffix += ":Shocked";
            if ((flags & StatusAilmentFlag.Bleeding) != 0) suffix += ":Bleeding";
            return suffix;
        }

        public static string FormatAilmentPhrase(StatusAilmentFlag flags)
        {
            if (flags == StatusAilmentFlag.None)
                return string.Empty;

            var parts = new List<string>(6);
            if ((flags & StatusAilmentFlag.Frozen) != 0) parts.Add("Frozen");
            if ((flags & StatusAilmentFlag.Stunned) != 0) parts.Add("Stunned");
            if ((flags & StatusAilmentFlag.Poisoned) != 0) parts.Add("Poisoned");
            if ((flags & StatusAilmentFlag.Ignited) != 0) parts.Add("Ignited");
            if ((flags & StatusAilmentFlag.Shocked) != 0) parts.Add("Shocked");
            if ((flags & StatusAilmentFlag.Bleeding) != 0) parts.Add("Bleeding");
            if (parts.Count == 0)
                return string.Empty;
            if (parts.Count == 1)
                return parts[0];
            if (parts.Count == 2)
                return parts[0] + " and " + parts[1];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + ", and " + parts[parts.Count - 1];
        }
    }


    /// <summary>
    /// CMS serializes one of these wherever content needs to reference a
    /// telemetry metric.
    /// </summary>
    [Serializable]
    public sealed class TelemetryStatReference
    {
        public TelemetryStatArgs args = null!;
    }


    // =========================================================================
    // EVALUATION CONTEXT
    // =========================================================================

    /// <summary>
    /// One window of combat events plus the window duration.
    /// Stats read only the fields they need.
    /// </summary>
    public readonly struct TelemetryStatEvalContext
    {
        public readonly NativeArray<DamageEvent> DamageEvents;
        public readonly NativeArray<TickDamageEvent> TickDamageEvents;
        public readonly NativeArray<StatusAilmentAppliedEvent> StatusAilmentEvents;
        public readonly NativeArray<AttackEntityRemovalEvent> AttackEntityRemovalEvents;
        public readonly float Duration;
        public readonly int SpellIdFilter;
        public readonly bool HasSpellFilter;

        public TelemetryStatEvalContext(
            NativeArray<DamageEvent> damageEvents,
            NativeArray<TickDamageEvent> tickDamageEvents,
            NativeArray<StatusAilmentAppliedEvent> statusAilmentEvents,
            NativeArray<AttackEntityRemovalEvent> attackEntityRemovalEvents,
            float duration,
            int spellIdFilter = 0,
            bool hasSpellFilter = false)
        {
            DamageEvents = damageEvents;
            TickDamageEvents = tickDamageEvents;
            StatusAilmentEvents = statusAilmentEvents;
            AttackEntityRemovalEvents = attackEntityRemovalEvents;
            Duration = duration;
            SpellIdFilter = spellIdFilter;
            HasSpellFilter = hasSpellFilter;
        }

        public bool MatchesSpell(int spellId)
        {
            return !HasSpellFilter || spellId == SpellIdFilter;
        }

        public bool Include(in DamageEvent evt, StatusAilmentFlag requiredAilments)
        {
            if (!MatchesSpell(evt.spellId))
                return false;
            return MatchesRequiredAilments(evt.enemyStatusAilments, requiredAilments);
        }

        public bool Include(in TickDamageEvent evt, StatusAilmentFlag requiredAilments)
        {
            if (!MatchesSpell(evt.spellId))
                return false;
            return MatchesRequiredAilments(evt.enemyStatusAilments, requiredAilments);
        }

        public bool Include(in StatusAilmentAppliedEvent evt)
        {
            return MatchesSpell(evt.spellId);
        }

        public static bool MatchesRequiredAilments(
            StatusAilmentFlag eventAilments,
            StatusAilmentFlag required)
        {
            return required == StatusAilmentFlag.None
                || (eventAilments & required) == required;
        }
    }


    // =========================================================================
    // TYPE-ERASED RUNTIME INTERFACE
    // =========================================================================

    public interface ITelemetryStat<TValue>
    {
        Type ArgsType { get; }

        /// <summary>
        /// True when frame results should be added into parent time windows.
        /// False for values that must be computed at read time (e.g. DPS).
        /// </summary>
        bool IsAdditive { get; }

        /// <summary>True when this query should also be folded per spellId.</summary>
        bool FoldPerSpell { get; }

        TValue ProcessEvents(TelemetryStatArgs args, in TelemetryStatEvalContext context);
    }


    // =========================================================================
    // STRONGLY-TYPED STAT BASE
    // =========================================================================

    public abstract class TelemetryStatBase<TValue, TArgs> :
        ITelemetryStat<TValue>
        where TArgs : TelemetryStatArgs
    {
        public Type ArgsType => typeof(TArgs);

        public virtual bool IsAdditive => true;

        public virtual bool FoldPerSpell => true;

        protected abstract TValue ProcessEventsTyped(
            TArgs args,
            in TelemetryStatEvalContext context);

        public TValue ProcessEvents(TelemetryStatArgs args, in TelemetryStatEvalContext context)
        {
            if (args is not TArgs typedArgs)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} expected arguments of type " +
                    $"{typeof(TArgs).Name}, but received {args.GetType().Name}.");
            }

            return ProcessEventsTyped(typedArgs, in context);
        }
    }


    // =========================================================================
    // REGISTRY
    // =========================================================================

    public sealed class TelemetryStatRegistry<TValue>
    {
        private readonly Dictionary<Type, ITelemetryStat<TValue>> _stats = new();

        public void Register<TStat, TArgs>()
            where TStat : TelemetryStatBase<TValue, TArgs>, new()
            where TArgs : TelemetryStatArgs
        {
            Type argsType = typeof(TArgs);

            if (_stats.ContainsKey(argsType))
            {
                throw new InvalidOperationException(
                    $"A telemetry stat has already been registered for " +
                    $"{argsType.Name}.");
            }

            _stats.Add(argsType, new TStat());
        }


        public ITelemetryStat<TValue> Resolve(TelemetryStatArgs args)
        {
            Type argsType = args.GetType();

            if (!_stats.TryGetValue(argsType, out ITelemetryStat<TValue>? stat))
            {
                throw new InvalidOperationException(
                    $"No telemetry stat has been registered for arguments " +
                    $"type {argsType.Name}.");
            }

            return stat;
        }


        public TValue Process(
            TelemetryStatReference reference,
            in TelemetryStatEvalContext context)
        {
            if (reference.args == null)
            {
                throw new InvalidOperationException(
                    "TelemetryStatReference contains no arguments.");
            }

            ITelemetryStat<TValue> stat = Resolve(reference.args);
            return stat.ProcessEvents(reference.args, in context);
        }


        public string GetKey(TelemetryStatReference reference)
        {
            if (reference.args == null)
            {
                throw new InvalidOperationException(
                    "TelemetryStatReference contains no arguments.");
            }

            return reference.args.GetKey();
        }
    }


    // =========================================================================
    // EVENT COUNTS (HITS / CRITS / KILLS)
    // =========================================================================

    public enum CombatEventCountKind : byte
    {
        Hits,
        Crits,
        Kills
    }


    [Serializable, MenuPath("Telemetry/Counts")]
    public sealed class EventCountTelemetryStatArgs : TelemetryStatArgs
    {
        public CombatEventCountKind kind;
        public StatusAilmentFlag requiredEnemyAilments;


        public override string GetKey()
        {
            return $"event_count:{kind}{AilmentKeySuffix(requiredEnemyAilments)}";
        }

        public override string GetDescription()
        {
            string metric = kind switch
            {
                CombatEventCountKind.Hits => "hits",
                CombatEventCountKind.Crits => "crits",
                CombatEventCountKind.Kills => "kills",
                _ => kind.ToString()
            };
            string ailments = FormatAilmentPhrase(requiredEnemyAilments);
            if (string.IsNullOrEmpty(ailments))
                return metric;
            return $"{metric} on {ailments} enemies";
        }
    }


    public sealed class EventCountTelemetryStat :
        TelemetryStatBase<float, EventCountTelemetryStatArgs>
    {
        protected override float ProcessEventsTyped(
            EventCountTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            return args.kind switch
            {
                CombatEventCountKind.Hits => CountHits(args, in context),
                CombatEventCountKind.Crits => CountCrits(args, in context),
                CombatEventCountKind.Kills => CountKills(args, in context),
                _ => throw new InvalidOperationException(
                    $"Unsupported combat event count kind {args.kind}.")
            };
        }


        static float CountHits(
            EventCountTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            int count = 0;
            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (context.Include(in damageEvent, args.requiredEnemyAilments))
                    count++;
            }

            return count;
        }


        static float CountCrits(
            EventCountTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            int count = 0;
            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (damageEvent.isCrit
                    && context.Include(in damageEvent, args.requiredEnemyAilments))
                    count++;
            }

            return count;
        }


        static float CountKills(
            EventCountTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            int count = 0;
            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (damageEvent.wasKill
                    && context.Include(in damageEvent, args.requiredEnemyAilments))
                    count++;
            }

            foreach (TickDamageEvent tickEvent in context.TickDamageEvents)
            {
                if (tickEvent.wasKill
                    && context.Include(in tickEvent, args.requiredEnemyAilments))
                    count++;
            }

            return count;
        }
    }


    // =========================================================================
    // AILMENT APPLICATIONS
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Ailments")]
    public sealed class AilmentApplicationTelemetryStatArgs :
        TelemetryStatArgs
    {
        public StatusAilmentFlag statusAilmentFlag;


        public override string GetKey()
        {
            return $"ailment_application:{statusAilmentFlag}";
        }

        public override string GetDescription()
        {
            string name = FormatAilmentPhrase(statusAilmentFlag);
            if (string.IsNullOrEmpty(name))
                return "ailment applications";
            return $"{name} applications";
        }
    }


    public sealed class AilmentApplicationTelemetryStat :
        TelemetryStatBase<float, AilmentApplicationTelemetryStatArgs>
    {
        protected override float ProcessEventsTyped(
            AilmentApplicationTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            int count = 0;

            foreach (StatusAilmentAppliedEvent statusEvent in context.StatusAilmentEvents)
            {
                if (!context.Include(in statusEvent))
                    continue;
                if (statusEvent.ailmentFlag == args.statusAilmentFlag)
                    count++;
            }

            return count;
        }
    }


    // =========================================================================
    // ATTACK ENTITIES EXPIRED
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Other")]
    public sealed class AttackEntitiesExpiredTelemetryStatArgs : TelemetryStatArgs
    {
        public override string GetKey()
        {
            return "attack_entities_expired";
        }

        public override string GetDescription() => "expired attack entities";
    }


    public sealed class AttackEntitiesExpiredTelemetryStat :
        TelemetryStatBase<float, AttackEntitiesExpiredTelemetryStatArgs>
    {
        public override bool FoldPerSpell => false;

        protected override float ProcessEventsTyped(
            AttackEntitiesExpiredTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            if (context.HasSpellFilter)
                return 0f;

            int count = 0;

            foreach (AttackEntityRemovalEvent removal in context.AttackEntityRemovalEvents)
            {
                if (IsExpiration(removal.reason))
                    count++;
            }

            return count;
        }


        static bool IsExpiration(AttackEntityRemovalReason reason)
        {
            return reason == AttackEntityRemovalReason.ExpiredByTime
                || reason == AttackEntityRemovalReason.ExpiredByDistance
                || reason == AttackEntityRemovalReason.ExpiredByFrames;
        }
    }


    // =========================================================================
    // DAMAGE (TOTAL AND BY TYPE)
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Damage")]
    public sealed class DamageTelemetryStatArgs : TelemetryStatArgs
    {
        public DamageType damageType;
        public StatusAilmentFlag requiredEnemyAilments;


        public override string GetKey()
        {
            string baseKey = damageType == DamageType.None
                ? "total_damage"
                : $"damage:{damageType}";
            return baseKey + AilmentKeySuffix(requiredEnemyAilments);
        }

        public override string GetDescription()
        {
            string metric = damageType switch
            {
                DamageType.None => "total damage",
                DamageType.Physical => "physical damage",
                DamageType.Fire => "fire damage",
                DamageType.Cold => "cold damage",
                DamageType.Lightning => "lightning damage",
                _ => $"{damageType} damage"
            };
            string ailments = FormatAilmentPhrase(requiredEnemyAilments);
            if (string.IsNullOrEmpty(ailments))
                return metric;
            return $"{metric} to {ailments} enemies";
        }
    }


    public sealed class DamageTelemetryStat :
        TelemetryStatBase<float, DamageTelemetryStatArgs>
    {
        protected override float ProcessEventsTyped(
            DamageTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            return Sum(in context, args.damageType, args.requiredEnemyAilments);
        }


        internal static float Sum(
            in TelemetryStatEvalContext context,
            DamageType damageType,
            StatusAilmentFlag requiredAilments = StatusAilmentFlag.None)
        {
            float total = 0f;

            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (context.Include(in damageEvent, requiredAilments))
                    total += Select(damageEvent, damageType);
            }

            foreach (TickDamageEvent tickEvent in context.TickDamageEvents)
            {
                if (context.Include(in tickEvent, requiredAilments))
                    total += Select(tickEvent, damageType);
            }

            return total;
        }


        static float Select(in DamageEvent evt, DamageType damageType)
        {
            return damageType switch
            {
                DamageType.None => evt.damageDealt,
                DamageType.Physical => evt.physicalDamage,
                DamageType.Fire => evt.fireDamage,
                DamageType.Cold => evt.coldDamage,
                DamageType.Lightning => evt.lightningDamage,
                _ => throw new InvalidOperationException(
                    $"Unsupported damage type {damageType}.")
            };
        }


        static float Select(in TickDamageEvent evt, DamageType damageType)
        {
            return damageType switch
            {
                DamageType.None => evt.damageDealt,
                DamageType.Physical => evt.physicalDamage,
                DamageType.Fire => evt.fireDamage,
                DamageType.Cold => evt.coldDamage,
                DamageType.Lightning => evt.lightningDamage,
                _ => throw new InvalidOperationException(
                    $"Unsupported damage type {damageType}.")
            };
        }
    }


    // =========================================================================
    // OVERKILL DAMAGE
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Damage")]
    public sealed class OverkillDamageTelemetryStatArgs : TelemetryStatArgs
    {
        public override string GetKey()
        {
            return "overkill_damage";
        }

        public override string GetDescription() => "overkill damage";
    }


    public sealed class OverkillDamageTelemetryStat :
        TelemetryStatBase<float, OverkillDamageTelemetryStatArgs>
    {
        protected override float ProcessEventsTyped(
            OverkillDamageTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            float total = 0f;

            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (damageEvent.wasKill && context.MatchesSpell(damageEvent.spellId))
                    total += damageEvent.overkillDamage;
            }

            foreach (TickDamageEvent tickEvent in context.TickDamageEvents)
            {
                if (tickEvent.wasKill && context.MatchesSpell(tickEvent.spellId))
                    total += tickEvent.overkillDamage;
            }

            return total;
        }
    }


    // =========================================================================
    // BLOOD EXTRACTED
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Other")]
    public sealed class BloodExtractedTelemetryStatArgs : TelemetryStatArgs
    {
        public override string GetKey()
        {
            return "blood_extracted";
        }

        public override string GetDescription() => "blood extracted";
    }


    public sealed class BloodExtractedTelemetryStat :
        TelemetryStatBase<float, BloodExtractedTelemetryStatArgs>
    {
        protected override float ProcessEventsTyped(
            BloodExtractedTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            float total = 0f;

            foreach (DamageEvent damageEvent in context.DamageEvents)
            {
                if (context.MatchesSpell(damageEvent.spellId))
                    total += damageEvent.bloodExtracted;
            }

            foreach (TickDamageEvent tickEvent in context.TickDamageEvents)
            {
                if (context.MatchesSpell(tickEvent.spellId))
                    total += tickEvent.bloodExtracted;
            }

            return total;
        }
    }


    // =========================================================================
    // DURATION
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Other")]
    public sealed class DurationTelemetryStatArgs : TelemetryStatArgs
    {
        public override string GetKey()
        {
            return "duration";
        }

        public override string GetDescription() => "duration";
    }


    public sealed class DurationTelemetryStat :
        TelemetryStatBase<float, DurationTelemetryStatArgs>
    {
        public override bool FoldPerSpell => false;

        protected override float ProcessEventsTyped(
            DurationTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            if (context.HasSpellFilter)
                return 0f;

            return context.Duration;
        }
    }


    // =========================================================================
    // DPS (window-derived; not accumulated)
    // =========================================================================

    [Serializable, MenuPath("Telemetry/Damage")]
    public sealed class DpsTelemetryStatArgs : TelemetryStatArgs
    {
        public override string GetKey()
        {
            return "dps";
        }

        public override string GetDescription() => "DPS";

        public override bool RequiresEventSubscription => false;
    }


    public sealed class DpsTelemetryStat :
        TelemetryStatBase<float, DpsTelemetryStatArgs>
    {
        public override bool IsAdditive => false;

        public override bool FoldPerSpell => false;

        protected override float ProcessEventsTyped(
            DpsTelemetryStatArgs args,
            in TelemetryStatEvalContext context)
        {
            if (context.Duration <= 0f)
                return 0f;

            return DamageTelemetryStat.Sum(in context, DamageType.None) / context.Duration;
        }
    }


    // =========================================================================
    // CORE ALWAYS-ON QUERIES + REGISTRY
    // =========================================================================

    public static class TelemetryCoreStats
    {
        public static readonly EventCountTelemetryStatArgs Hits = new()
        {
            kind = CombatEventCountKind.Hits
        };

        public static readonly EventCountTelemetryStatArgs Crits = new()
        {
            kind = CombatEventCountKind.Crits
        };

        public static readonly EventCountTelemetryStatArgs Kills = new()
        {
            kind = CombatEventCountKind.Kills
        };

        public static readonly DamageTelemetryStatArgs TotalDamage = new()
        {
            damageType = DamageType.None
        };

        public static readonly DamageTelemetryStatArgs PhysicalDamage = new()
        {
            damageType = DamageType.Physical
        };

        public static readonly DamageTelemetryStatArgs FireDamage = new()
        {
            damageType = DamageType.Fire
        };

        public static readonly DamageTelemetryStatArgs ColdDamage = new()
        {
            damageType = DamageType.Cold
        };

        public static readonly DamageTelemetryStatArgs LightningDamage = new()
        {
            damageType = DamageType.Lightning
        };

        public static readonly OverkillDamageTelemetryStatArgs Overkill = new();

        public static readonly BloodExtractedTelemetryStatArgs BloodExtracted = new();

        public static readonly AttackEntitiesExpiredTelemetryStatArgs AttackEntitiesExpired = new();

        public static readonly AilmentApplicationTelemetryStatArgs FrozenApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Frozen
        };

        public static readonly AilmentApplicationTelemetryStatArgs IgnitedApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Ignited
        };

        public static readonly AilmentApplicationTelemetryStatArgs ShockedApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Shocked
        };

        public static readonly AilmentApplicationTelemetryStatArgs PoisonedApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Poisoned
        };

        public static readonly AilmentApplicationTelemetryStatArgs StunnedApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Stunned
        };

        public static readonly AilmentApplicationTelemetryStatArgs BleedingApplied = new()
        {
            statusAilmentFlag = StatusAilmentFlag.Bleeding
        };

        public static readonly DurationTelemetryStatArgs Duration = new();

        public static readonly DpsTelemetryStatArgs Dps = new();

        public static readonly string HitsKey;
        public static readonly string CritsKey;
        public static readonly string KillsKey;
        public static readonly string TotalDamageKey;
        public static readonly string PhysicalDamageKey;
        public static readonly string FireDamageKey;
        public static readonly string ColdDamageKey;
        public static readonly string LightningDamageKey;
        public static readonly string OverkillKey;
        public static readonly string BloodExtractedKey;
        public static readonly string AttackEntitiesExpiredKey;
        public static readonly string FrozenAppliedKey;
        public static readonly string IgnitedAppliedKey;
        public static readonly string ShockedAppliedKey;
        public static readonly string PoisonedAppliedKey;
        public static readonly string StunnedAppliedKey;
        public static readonly string BleedingAppliedKey;
        public static readonly string DurationKey;
        public static readonly string DpsKey;

        public static readonly TelemetryStatArgs[] Additive =
        {
            Hits,
            Crits,
            Kills,
            TotalDamage,
            PhysicalDamage,
            FireDamage,
            ColdDamage,
            LightningDamage,
            Overkill,
            BloodExtracted,
            AttackEntitiesExpired,
            FrozenApplied,
            IgnitedApplied,
            ShockedApplied,
            PoisonedApplied,
            StunnedApplied,
            BleedingApplied,
            Duration
        };

        static readonly HashSet<string> CoreKeys;

        static TelemetryCoreStats()
        {
            HitsKey = Hits.GetKey();
            CritsKey = Crits.GetKey();
            KillsKey = Kills.GetKey();
            TotalDamageKey = TotalDamage.GetKey();
            PhysicalDamageKey = PhysicalDamage.GetKey();
            FireDamageKey = FireDamage.GetKey();
            ColdDamageKey = ColdDamage.GetKey();
            LightningDamageKey = LightningDamage.GetKey();
            OverkillKey = Overkill.GetKey();
            BloodExtractedKey = BloodExtracted.GetKey();
            AttackEntitiesExpiredKey = AttackEntitiesExpired.GetKey();
            FrozenAppliedKey = FrozenApplied.GetKey();
            IgnitedAppliedKey = IgnitedApplied.GetKey();
            ShockedAppliedKey = ShockedApplied.GetKey();
            PoisonedAppliedKey = PoisonedApplied.GetKey();
            StunnedAppliedKey = StunnedApplied.GetKey();
            BleedingAppliedKey = BleedingApplied.GetKey();
            DurationKey = Duration.GetKey();
            DpsKey = Dps.GetKey();

            CoreKeys = new HashSet<string>(Additive.Length + 1);
            for (int i = 0; i < Additive.Length; i++)
                CoreKeys.Add(Additive[i].GetKey());
            CoreKeys.Add(DpsKey);
        }

        public static bool IsCoreKey(string key) => CoreKeys.Contains(key);
    }


    public static class TelemetryStatRegistryFactory
    {
        public static TelemetryStatRegistry<float> Create()
        {
            var registry = new TelemetryStatRegistry<float>();

            registry.Register<
                EventCountTelemetryStat,
                EventCountTelemetryStatArgs>();

            registry.Register<
                AilmentApplicationTelemetryStat,
                AilmentApplicationTelemetryStatArgs>();

            registry.Register<
                AttackEntitiesExpiredTelemetryStat,
                AttackEntitiesExpiredTelemetryStatArgs>();

            registry.Register<
                DamageTelemetryStat,
                DamageTelemetryStatArgs>();

            registry.Register<
                OverkillDamageTelemetryStat,
                OverkillDamageTelemetryStatArgs>();

            registry.Register<
                BloodExtractedTelemetryStat,
                BloodExtractedTelemetryStatArgs>();

            registry.Register<
                DurationTelemetryStat,
                DurationTelemetryStatArgs>();

            registry.Register<
                DpsTelemetryStat,
                DpsTelemetryStatArgs>();

            return registry;
        }
    }
}
