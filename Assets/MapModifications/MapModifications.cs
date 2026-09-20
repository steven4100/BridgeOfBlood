using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System;



// These classes are meant to service a new scene where a user will select from a collection of map modifiers and apply them to the map. For instance
// there may be a "freeze" map modifier that the user select, and then spawns it on the map. Under the hood this will be an attack entity with a freeze 
// behavior. At the start of every combat round. these map modifiers will be re-applied to create the illusion of a permanently modified map.
//
//THese map modifiers will be applied all at once at the start of the simulation. Currently i foresee only two concrete examples, but we should
//support any arbitrary amount
// 1) on time attack entity spawns, so for the end user it will just seem like the mobs start with certain ailments etc.
// 2) a modification that combines a region of enemies into a single enemy and upping its health.
public abstract class MapModifier
{
    //the order in which to apply map mods
    public int index;

    public abstract void Apply(MapModifierContext context);
}

//This is sort of a lightweight version of spell casting. Ultimately it exists to drive consumable items that are a one time spell spawn.
public class AttackEntitySpawnMapModifier : MapModifier
{
    AttackEntityData attackEntityData;
    IAttackEntityEmitter emitter;
    public AttackEntitySpawnMapModifier(AttackEntityData attackEntityData, IAttackEntityEmitter emitter)
    {

    }

    public override void Apply(MapModifierContext context)
    {
        
    }
}


public class MapModifierContext
{

}

public static class MapModifierApplicator
{
    public static void ApplyMapModifiers(List<MapModifier> modifiers, AttackEntityManager attackEntityManager, EnemyManager enemyManager)
    {

    }
}


public interface IAttackEntityEmitter
{
    public void Emit(AttackEntityData attackEntityData);
}
public abstract class MapModifierUI : MonoBehaviour
{
    public abstract void Bind(MapModifier modifier);
}
public abstract class MapModifierUI<TModifier> : MapModifierUI
    where TModifier : MapModifier
{
    public sealed override void Bind(MapModifier modifier)
    {
        Bind((TModifier)modifier);
    }

    protected abstract void Bind(TModifier modifier);
}

public sealed class AttackEntitySpawnMapModifierUI
    : MapModifierUI<AttackEntitySpawnMapModifier>
{
    protected override void Bind(
        AttackEntitySpawnMapModifier modifier)
    {
        // Populate labels, icons, etc.
    }
}

//This should act as an abstraction for recieving ui input and providing information for the emitter.
//Currently i foresee only two types of controls. Emitters that just need a position, and emitters that need a position and a direction
public interface IEmitterController { }
public sealed class MapModifierController : MonoBehaviour
{
    [SerializeField]
    private Transform layoutRoot;

    private MapModifierUIRegistry registry;

    public void Initialize(MapModifierUIRegistry registry)
    {
        this.registry = registry;
    }

    public void ShowModifiers(
        IReadOnlyList<MapModifier> modifiers)
    {
        foreach (MapModifier modifier in modifiers)
        {
            MapModifierUI prefab =
                registry.GetPrefab(modifier);

            MapModifierUI ui =
                Instantiate(prefab, layoutRoot);

            ui.Bind(modifier);
        }
    }
}

public sealed class MapModifierUIRegistry
{
    private readonly Dictionary<Type, MapModifierUI> prefabs = new();

    public void Register<TModifier>(
        MapModifierUI<TModifier> prefab)
        where TModifier : MapModifier
    {
        prefabs[typeof(TModifier)] = prefab;
    }

    public MapModifierUI GetPrefab(MapModifier modifier)
    {
        Type modifierType = modifier.GetType();

        if (!prefabs.TryGetValue(
                modifierType,
                out MapModifierUI prefab))
        {
            throw new InvalidOperationException(
                $"No UI registered for {modifierType.Name}");
        }

        return prefab;
    }
}