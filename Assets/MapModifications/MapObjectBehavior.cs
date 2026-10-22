using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Reaction attached to one placed map object. Subclasses own their counters.
/// Cloned onto the live object each time the map is rebuilt.
/// </summary>
[Serializable]
public abstract class MapObjectBehavior
{
    public abstract MapObjectBehavior Clone();

    public virtual void OnHit(in MapObjectHit hit, MapObjectCombatContext context)
    {
    }

    public virtual void Tick(in MapObjectTickContext context)
    {
    }

    /// <summary>Applied to the frame hit list after <see cref="OnHit"/>. Null when this behavior does not edit hits.</summary>
    public virtual HitModifier HitModifier => null;
}

public struct MapObjectHit
{
    public EntityId AttackEntityId;
    public int AttackIndex;
    public float Damage;
}

public struct MapObjectCombatContext
{
    public float2 ObjectPosition;
    public float SimulationTime;
    public MapObjectSpellCast Caster;

    public void CastSpell(SpellAuthoringData spell, int times)
    {
        Caster.Cast(spell, ObjectPosition, SimulationTime, times);
    }
}

public struct MapObjectTickContext
{
    public float DeltaTime;
    public float SimulationTime;
    public float2 Position;
    public MapObjectSpellCast Caster;

    public void CastSpell(SpellAuthoringData spell, int times)
    {
        Caster.Cast(spell, Position, SimulationTime, times);
    }
}

/// <summary>
/// Plays authored spells from map objects through the same keyframe emission path as a player cast.
/// </summary>
public sealed class MapObjectSpellCast
{
    SpellInvoker _invoker;
    SpellEmissionHandler _handler;
    readonly Dictionary<SpellAuthoringData, RuntimeSpell> _runtimes = new Dictionary<SpellAuthoringData, RuntimeSpell>();

    public void Bind(AttackEntityManager attacks)
    {
        if (_handler != null)
            return;

        _handler = new SpellEmissionHandler(attacks);
        _invoker = new SpellInvoker(_handler);
    }

    public void Cast(SpellAuthoringData spell, float2 origin, float simulationTime, int times)
    {
        if (times <= 0)
            return;

        if (!_runtimes.TryGetValue(spell, out RuntimeSpell runtime))
        {
            runtime = new RuntimeSpell(spell);
            _runtimes[spell] = runtime;
        }

        for (int i = 0; i < times; i++)
            _invoker.StartCast(runtime, origin, simulationTime, runtime.spellId, i);
    }

    public void Update(float simulationTime)
    {
        if (_invoker == null)
            return;

        _invoker.Update(simulationTime, new float2(1f, 0f));
        _handler.Update(simulationTime);
    }

    public void Clear()
    {
        _invoker?.ClearActiveCasts();
        _handler?.ClearPendingSpawns();
        _runtimes.Clear();
    }
}

/// <summary>
/// Live map objects for the current field. Cleared on round reset and refilled when modifiers apply.
/// </summary>
public sealed class MapObjectRegistry
{
    sealed class LiveMapObject
    {
        public EntityId Id;
        public MapObjectBehavior[] Behaviors;
    }

    readonly List<LiveMapObject> _live = new List<LiveMapObject>();
    readonly MapObjectSpellCast _caster = new MapObjectSpellCast();

    public void Clear()
    {
        _live.Clear();
        _caster.Clear();
    }

    public void Register(EntityId id, IReadOnlyList<MapObjectBehavior> templates)
    {
        int count = templates != null ? templates.Count : 0;
        var cloned = new MapObjectBehavior[count];
        for (int i = 0; i < count; i++)
            cloned[i] = templates[i].Clone();
        _live.Add(new LiveMapObject { Id = id, Behaviors = cloned });
    }

    public void ProcessHits(
        NativeList<HitEvent> hits,
        EnemyBuffers enemies,
        AttackEntityManager attacks,
        float simulationTime)
    {
        _caster.Bind(attacks);
        if (_live.Count == 0 || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            HitEvent hit = hits[i];
            int enemyIndex = hit.enemyEntityId.Index;
            if (enemies.Ids[enemyIndex].IsMapObject == 0)
                continue;

            LiveMapObject live = Find(hit.enemyEntityId);
            if (live == null)
                continue;

            var mapHit = new MapObjectHit
            {
                AttackEntityId = hit.attackEntityId,
                AttackIndex = hit.attackEntityId.Index,
                Damage = hit.physicalDamage + hit.coldDamage + hit.fireDamage + hit.lightningDamage
            };
            var context = new MapObjectCombatContext
            {
                ObjectPosition = enemies.Motion[enemyIndex].position,
                SimulationTime = simulationTime,
                Caster = _caster
            };
            MapObjectBehavior[] behaviors = live.Behaviors;
            for (int b = 0; b < behaviors.Length; b++)
                behaviors[b].OnHit(in mapHit, context);
        }
    }

    public void ApplyHitModifiers(NativeList<HitEvent> hits, EnemyBuffers enemies)
    {
        if (_live.Count == 0 || hits.Length == 0)
            return;

        for (int i = 0; i < _live.Count; i++)
        {
            MapObjectBehavior[] behaviors = _live[i].Behaviors;
            for (int b = 0; b < behaviors.Length; b++)
            {
                HitModifier modifier = behaviors[b].HitModifier;
                if (modifier != null)
                    modifier.Modify(hits, enemies, 0);
            }
        }
    }

    public void Tick(float deltaTime, float simulationTime, AttackEntityManager attacks, EnemyBuffers enemies)
    {
        _caster.Bind(attacks);
        if (_live.Count > 0)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                LiveMapObject live = _live[i];
                if (!enemies.IsValid(live.Id))
                    continue;

                var context = new MapObjectTickContext
                {
                    DeltaTime = deltaTime,
                    SimulationTime = simulationTime,
                    Position = enemies.Motion[live.Id.Index].position,
                    Caster = _caster
                };
                MapObjectBehavior[] behaviors = live.Behaviors;
                for (int b = 0; b < behaviors.Length; b++)
                    behaviors[b].Tick(in context);
            }
        }

        _caster.Update(simulationTime);
    }

    LiveMapObject Find(EntityId id)
    {
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i].Id == id)
                return _live[i];
        }

        return null;
    }
}
