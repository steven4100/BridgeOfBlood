using System;
using System.Collections;
using System.Collections.Generic;

namespace BridgeOfBlood.Data.Spells
{
    /// <summary>Read-only typed view over one exact modification-data bucket.</summary>
    public readonly struct ModificationCollection<T> : IReadOnlyList<T>
        where T : IModificationData
    {
        readonly List<IModificationData> values;

        internal ModificationCollection(List<IModificationData> values)
        {
            this.values = values;
        }

        public int Count => values?.Count ?? 0;
        public T this[int index] => (T)values[index];

        public Enumerator GetEnumerator() => new Enumerator(values);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            readonly List<IModificationData> values;
            int index;

            internal Enumerator(List<IModificationData> values)
            {
                this.values = values;
                index = -1;
            }

            public T Current => (T)values[index];
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int next = index + 1;
                if (values == null || next >= values.Count)
                    return false;
                index = next;
                return true;
            }

            public void Reset() => index = -1;
            public void Dispose() { }
        }
    }

    /// <summary>
    /// All modifications resolved for one spell. Data is indexed by its exact runtime type so
    /// consumers fetch only the channel they own.
    /// </summary>
    [Serializable]
    public sealed class ModificationSet
    {
        readonly Dictionary<Type, List<IModificationData>> byType =
            new Dictionary<Type, List<IModificationData>>();

        public readonly List<AttackEntityModifier> attackEntityModifiers =
            new List<AttackEntityModifier>();

        public bool IsEmpty => byType.Count == 0 && attackEntityModifiers.Count == 0;

        public void Add(IModificationData modification)
        {
            Type type = modification.GetType();
            if (!byType.TryGetValue(type, out List<IModificationData> values))
            {
                values = new List<IModificationData>();
                byType[type] = values;
            }

            values.Add(modification);
        }

        public void Add(AttackEntityModifier modifier) =>
            attackEntityModifiers.Add(modifier);

        public ModificationCollection<T> Get<T>() where T : IModificationData
        {
            byType.TryGetValue(typeof(T), out List<IModificationData> values);
            return new ModificationCollection<T>(values);
        }

        public void Clear()
        {
            byType.Clear();
            attackEntityModifiers.Clear();
        }

        public void MergeFrom(ModificationSet other)
        {
            foreach (KeyValuePair<Type, List<IModificationData>> entry in other.byType)
            {
                if (!byType.TryGetValue(entry.Key, out List<IModificationData> values))
                {
                    values = new List<IModificationData>();
                    byType[entry.Key] = values;
                }

                values.AddRange(entry.Value);
            }

            attackEntityModifiers.AddRange(other.attackEntityModifiers);
        }

        public ModificationSet Clone()
        {
            var copy = new ModificationSet();
            copy.MergeFrom(this);
            return copy;
        }
    }

    /// <summary>
    /// Frame-scoped modification data: one global set plus per-runtime-spell sets.
    /// Finalization builds stable global + per-spell read snapshots for the rest of the frame.
    /// </summary>
    public sealed class ModificationCatalog
    {
        public ModificationSet Global { get; } = new ModificationSet();

        readonly Dictionary<int, ModificationSet> perSpellId =
            new Dictionary<int, ModificationSet>();
        readonly Dictionary<int, ModificationSet> resolvedBySpellId =
            new Dictionary<int, ModificationSet>();

        public void ResetForFrame()
        {
            Global.Clear();
            perSpellId.Clear();
            resolvedBySpellId.Clear();
        }

        public ModificationSet ForSpell(int spellId)
        {
            if (!perSpellId.TryGetValue(spellId, out ModificationSet modifications))
            {
                modifications = new ModificationSet();
                perSpellId[spellId] = modifications;
            }

            return modifications;
        }

        public void FinalizeResolution(IReadOnlyList<RuntimeSpell> spells)
        {
            resolvedBySpellId.Clear();

            for (int i = 0; i < spells.Count; i++)
                resolvedBySpellId[spells[i].spellId] = BuildMerged(spells[i].spellId);
        }

        public ModificationSet ResolveFor(int spellId)
        {
            if (resolvedBySpellId.TryGetValue(spellId, out ModificationSet resolved))
                return resolved;
            return Global;
        }

        ModificationSet BuildMerged(int spellId)
        {
            perSpellId.TryGetValue(spellId, out ModificationSet specific);
            bool hasGlobal = !Global.IsEmpty;
            bool hasSpecific = specific != null && !specific.IsEmpty;

            if (hasGlobal && hasSpecific)
            {
                var merged = new ModificationSet();
                merged.MergeFrom(Global);
                merged.MergeFrom(specific);
                return merged;
            }

            if (hasSpecific)
                return specific;
            return Global;
        }
    }
}
