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
    public EntityId ObjectId;
    public float2 ObjectPosition;
    public float SimulationTime;
    public MapObjectSpellCast Caster;

    public void CastSpell(SpellAuthoringData spell, int times)
    {
        Caster.Cast(spell, ObjectId, ObjectPosition, SimulationTime, times);
    }
}

public struct MapObjectTickContext
{
    public EntityId ObjectId;
    public float DeltaTime;
    public float SimulationTime;
    public float2 Position;
    public MapObjectSpellCast Caster;

    public void CastSpell(SpellAuthoringData spell, int times)
    {
        Caster.Cast(spell, ObjectId, Position, SimulationTime, times);
    }
}

/// <summary>
/// Plays authored spells from map objects through the same emission path as a player cast.
/// </summary>
public sealed class MapObjectSpellCast
{
    SpellEmissionHandler _handler;
    EnemyEmissionTargetProvider _targets;

    public void Bind(AttackEntityManager attacks, EnemyManager enemies)
    {
        if (_handler != null)
            return;

        _targets = new EnemyEmissionTargetProvider(enemies);
        _handler = new SpellEmissionHandler(attacks, _targets);
    }

    public void Cast(SpellAuthoringData spell, EntityId source, float2 origin, float simulationTime, int times)
    {
        if (times <= 0)
            return;

        var runtime = new RuntimeSpell(spell);
        runtime.ExcludedTarget = source;

        for (int i = 0; i < times; i++)
            _handler.Emit(runtime, origin, new float2(1f, 0f), simulationTime, runtime.spellId, i);
    }

    public void Update(float simulationTime)
    {
        if (_handler == null)
            return;

        _handler.Update(simulationTime);
    }

    public void Clear()
    {
        _handler?.ClearPendingSpawns();
    }

    public void Dispose()
    {
        Clear();
        _targets?.Dispose();
        _targets = null;
        _handler = null;
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

    public void Dispose()
    {
        Clear();
        _caster.Dispose();
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
        EnemyManager enemyManager,
        float simulationTime)
    {
        _caster.Bind(attacks, enemyManager);
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
                ObjectId = live.Id,
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

    public void Tick(
        float deltaTime,
        float simulationTime,
        AttackEntityManager attacks,
        EnemyManager enemyManager,
        EnemyBuffers enemies)
    {
        _caster.Bind(attacks, enemyManager);
        if (_live.Count > 0)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                LiveMapObject live = _live[i];
                if (!enemies.IsValid(live.Id))
                    continue;

                var context = new MapObjectTickContext
                {
                    ObjectId = live.Id,
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
