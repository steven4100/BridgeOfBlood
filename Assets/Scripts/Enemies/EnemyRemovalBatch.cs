using System;
using Unity.Collections;

/// <summary>
/// Frame-scratch buffers for pending enemy despawns, split by <see cref="EnemyRemovalReason"/>.
/// Each track keeps index lists sorted ascending (by construction); entity id at <c>i</c> matches index at <c>i</c>.
/// </summary>
public sealed class EnemyRemovalBatch : IDisposable
{
    public NativeList<int> CulledPastBoundsIndices { get; private set; }
    public NativeList<int> CulledPastBoundsEntityIds { get; private set; }
    public NativeList<int> HealthDepletedIndices { get; private set; }
    public NativeList<int> HealthDepletedEntityIds { get; private set; }

    public EnemyRemovalBatch(Allocator allocator = Allocator.Persistent)
    {
        CulledPastBoundsIndices = new NativeList<int>(64, allocator);
        CulledPastBoundsEntityIds = new NativeList<int>(64, allocator);
        HealthDepletedIndices = new NativeList<int>(64, allocator);
        HealthDepletedEntityIds = new NativeList<int>(64, allocator);
    }

    public void Clear()
    {
        CulledPastBoundsIndices.Clear();
        CulledPastBoundsEntityIds.Clear();
        HealthDepletedIndices.Clear();
        HealthDepletedEntityIds.Clear();
    }

    public void Dispose()
    {
        if (CulledPastBoundsIndices.IsCreated) CulledPastBoundsIndices.Dispose();
        if (CulledPastBoundsEntityIds.IsCreated) CulledPastBoundsEntityIds.Dispose();
        if (HealthDepletedIndices.IsCreated) HealthDepletedIndices.Dispose();
        if (HealthDepletedEntityIds.IsCreated) HealthDepletedEntityIds.Dispose();
    }
}
