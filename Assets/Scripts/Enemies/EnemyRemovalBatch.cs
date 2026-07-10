using System;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

/// <summary>
/// Frame-scratch buffers for pending enemy despawns, split by <see cref="EnemyRemovalReason"/>.
/// </summary>
public sealed class EnemyRemovalBatch : IDisposable
{
    public NativeList<EntityId> CulledPastBoundsEntityIds { get; private set; }
    public NativeList<EntityId> HealthDepletedEntityIds { get; private set; }

    public EnemyRemovalBatch(Allocator allocator = Allocator.Persistent)
    {
        CulledPastBoundsEntityIds = new NativeList<EntityId>(64, allocator);
        HealthDepletedEntityIds = new NativeList<EntityId>(64, allocator);
    }

    public void Clear()
    {
        CulledPastBoundsEntityIds.Clear();
        HealthDepletedEntityIds.Clear();
    }

    public void Dispose()
    {
        if (CulledPastBoundsEntityIds.IsCreated) CulledPastBoundsEntityIds.Dispose();
        if (HealthDepletedEntityIds.IsCreated) HealthDepletedEntityIds.Dispose();
    }
}
