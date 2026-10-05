using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A placed, ordered mutation of the freshly spawned map. Runtime instances are rebuilt from
/// <see cref="MapModifierDefinition"/> authoring assets and reapplied after every simulation reset.
/// </summary>
public abstract class MapModifier
{
    static int _nextIndex;

    /// <summary>
    /// Monotonic creation stamp. Incremented once per constructed modifier so the first-created
    /// instance is always the first evaluated, even if a later collection is reordered.
    /// </summary>
    public readonly int index;

    protected MapModifier()
    {
        index = _nextIndex++;
    }

    public abstract IEnumerable<CombatSimWait> Apply(MapModifierContext context);
}

/// <summary>Required simulation collaborators available to every map modifier.</summary>
public sealed class MapModifierContext
{
    public AttackEntityManager AttackEntityManager { get; }
    public EnemyManager EnemyManager { get; }
    public Rect Playfield { get; }

    public MapModifierContext(
        AttackEntityManager attackEntityManager,
        EnemyManager enemyManager,
        Rect playfield)
    {
        AttackEntityManager = attackEntityManager;
        EnemyManager = enemyManager;
        Playfield = playfield;
    }
}

/// <summary>Applies any number of map modifiers in deterministic index order.</summary>
public static class MapModifierApplicator
{
    public static void ApplyMapModifiers(
        IReadOnlyList<MapModifier> modifiers,
        GameSimulation simulation)
    {
        var ordered = new List<MapModifier>(modifiers.Count);
        for (int i = 0; i < modifiers.Count; i++)
            ordered.Add(modifiers[i]);

        ordered.Sort((left, right) => left.index.CompareTo(right.index));

        var context = new MapModifierContext(
            simulation.AttackEntityManager,
            simulation.EnemyManager,
            simulation.State.Playfield);
        for (int i = 0; i < ordered.Count; i++)
        {
            foreach (CombatSimWait wait in ordered[i].Apply(context))
                CombatSimWaitRunner.Run(simulation, wait);
        }
    }
}
