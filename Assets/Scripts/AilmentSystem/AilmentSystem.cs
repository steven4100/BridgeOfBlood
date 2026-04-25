using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class AilmentSystem
{
    private FrozenApplicationSystem _frozenSystem;
    private IgnitedApplicationSystem _ignitedSystem;
    private ShockedApplicationSystem _shockedSystem;
    private PoisonedApplicationSystem _poisonedSystem;
    private StunnedApplicationSystem _stunnedSystem;
    private BleedApplicationSystem _bleedSystem;

    private NativeList<EnemyBleedStatus> _enemyBleedStatus;
    private NativeList<EnemyFrozenStatus> _enemyFrozenStatus;
    private NativeList<EnemyIgniteStatus> _enemyIgniteStatus;
    private NativeList<EnemyStunnedStatus> _enemyStunnedStatus;
    private NativeList<EnemyPoisonStatus> _enemyPoisonStatus;
    private NativeList<EnemyShockedStatus> _enemyShockedStatus;

    private NativeHashMap<int, StatusAilmentFlag> _entityStatusAilmentFlagsScratch;

    private readonly HashSet<int> _removedEntityScratch = new HashSet<int>();

    public AilmentSystem()
    {
        _frozenSystem = new FrozenApplicationSystem();
        _ignitedSystem = new IgnitedApplicationSystem();
        _shockedSystem = new ShockedApplicationSystem();
        _poisonedSystem = new PoisonedApplicationSystem();
        _stunnedSystem = new StunnedApplicationSystem();
        _bleedSystem = new BleedApplicationSystem();

        _enemyBleedStatus = new NativeList<EnemyBleedStatus>(Allocator.Persistent);
        _enemyFrozenStatus = new NativeList<EnemyFrozenStatus>(Allocator.Persistent);
        _enemyIgniteStatus = new NativeList<EnemyIgniteStatus>(Allocator.Persistent);
        _enemyStunnedStatus = new NativeList<EnemyStunnedStatus>(Allocator.Persistent);
        _enemyPoisonStatus = new NativeList<EnemyPoisonStatus>(Allocator.Persistent);
        _enemyShockedStatus = new NativeList<EnemyShockedStatus>(Allocator.Persistent);
        _entityStatusAilmentFlagsScratch = new NativeHashMap<int, StatusAilmentFlag>(256, Allocator.Persistent);
    }

    public void Dispose()
    {
        if (_enemyBleedStatus.IsCreated) _enemyBleedStatus.Dispose();
        if (_enemyFrozenStatus.IsCreated) _enemyFrozenStatus.Dispose();
        if (_enemyIgniteStatus.IsCreated) _enemyIgniteStatus.Dispose();
        if (_enemyStunnedStatus.IsCreated) _enemyStunnedStatus.Dispose();
        if (_enemyPoisonStatus.IsCreated) _enemyPoisonStatus.Dispose();
        if (_enemyShockedStatus.IsCreated) _enemyShockedStatus.Dispose();
        if (_entityStatusAilmentFlagsScratch.IsCreated) _entityStatusAilmentFlagsScratch.Dispose();
    }

    /// <summary>
    /// Steps remaining ailment duration, drops expired tracker rows, and syncs enemy status flags from trackers.
    /// </summary>
    public NativeList<EnemyIgniteStatus> IgniteStatus => _enemyIgniteStatus;
    public NativeList<EnemyPoisonStatus> PoisonStatus => _enemyPoisonStatus;
    public NativeList<EnemyBleedStatus> BleedStatus => _enemyBleedStatus;

    public void BuildShockDamageMultipliers(NativeHashMap<int, float> outMap)
    {
        outMap.Clear();
        for (int i = 0; i < _enemyShockedStatus.Length; i++)
        {
            EnemyShockedStatus row = _enemyShockedStatus[i];
            if (row.lifetime <= 0f)
                continue;
            int id = row.entityID;
            if (outMap.TryGetValue(id, out float existing))
                outMap[id] = math.max(existing, row.damagerMultiplier);
            else
                outMap[id] = row.damagerMultiplier;
        }
    }

    public void TickStatusAilmentDurations(EnemyBuffers enemies, float deltaTime)
    {
        AilmentTimeScheduler.Tick(
            enemies.EntityIds,
            enemies.Status,
            deltaTime,
            _enemyBleedStatus,
            _enemyFrozenStatus,
            _enemyIgniteStatus,
            _enemyStunnedStatus,
            _enemyPoisonStatus,
            _enemyShockedStatus,
            _entityStatusAilmentFlagsScratch);
    }

    public void ProcessAilmentApplication(
        EnemyBuffers enemies,
        NativeArray<DamageEvent> damageEvents,
        AilmentApplierProvider ailmentAppliers,
        NativeList<StatusAilmentAppliedEvent> statusAilmentAppliedEvents,
        float ailmentTime)
    {
        int hitCount = damageEvents.Length;
        if (hitCount == 0)
            return;

        uint ailmentSeed = (uint)(ailmentTime * 10000f).GetHashCode();

        JobHandle h1 = _ignitedSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetIgnitedAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyIgniteStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed + 10000u,
            default);
        JobHandle h2 = _shockedSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetShockedAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyShockedStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed + 20000u,
            h1);
        JobHandle h3 = _poisonedSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetPoisonedAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyPoisonStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed + 30000u,
            h2);
        JobHandle h4 = _stunnedSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetStunnedAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyStunnedStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed + 40000u,
            h3);
        JobHandle h5 = _frozenSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetFrozenAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyFrozenStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed,
            h4);
        JobHandle h6 = _bleedSystem.ScheduleTrack(
            damageEvents,
            ailmentAppliers.GetBleedAppliers(),
            enemies.EntityIds,
            enemies.Status,
            _enemyBleedStatus,
            statusAilmentAppliedEvents,
            ailmentTime,
            ailmentSeed + 50000u,
            h5);
        h6.Complete();
    }

    /// <summary>
    /// Drops ailment tracker rows for the given removed enemy entity ids.
    /// </summary>
    public void ProcessEnemyRemovals(NativeList<int> removedEntityIds)
    {
        if (removedEntityIds.Length == 0)
            return;

        _removedEntityScratch.Clear();
        for (int i = 0; i < removedEntityIds.Length; i++)
            _removedEntityScratch.Add(removedEntityIds[i]);

        CompactTracker(_enemyBleedStatus, _removedEntityScratch, static r => r.entityID);
        CompactTracker(_enemyFrozenStatus, _removedEntityScratch, static r => r.entityID);
        CompactTracker(_enemyIgniteStatus, _removedEntityScratch, static r => r.entityID);
        CompactTracker(_enemyStunnedStatus, _removedEntityScratch, static r => r.entityID);
        CompactTracker(_enemyPoisonStatus, _removedEntityScratch, static r => r.entityID);
        CompactTracker(_enemyShockedStatus, _removedEntityScratch, static r => r.entityID);
    }

    private static void CompactTracker<T>(NativeList<T> list, HashSet<int> removed, Func<T, int> getEntityId) where T : unmanaged
    {
        int write = 0;
        for (int read = 0; read < list.Length; read++)
        {
            T row = list[read];
            if (!removed.Contains(getEntityId(row)))
            {
                if (write != read)
                    list[write] = row;
                write++;
            }
        }
        list.Resize(write, NativeArrayOptions.UninitializedMemory);
    }

    public interface AilmentApplierProvider
    {
        public NativeArray<FrozenApplierRuntime> GetFrozenAppliers();
        public NativeArray<IgnitedApplierRuntime> GetIgnitedAppliers();
        public NativeArray<ShockedApplierRuntime> GetShockedAppliers();
        public NativeArray<PoisonedApplierRuntime> GetPoisonedAppliers();
        public NativeArray<StunnedApplierRuntime> GetStunnedAppliers();
        public NativeArray<BleedApplierRuntime> GetBleedAppliers();
    }
}
