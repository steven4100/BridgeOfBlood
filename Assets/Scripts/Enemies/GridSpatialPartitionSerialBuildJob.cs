using BridgeOfBlood.Data.Enemies;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Single-threaded Burst job: histogram with per-enemy cell cache, exclusive prefix into starts,
/// scatter enemy indices into temp by cached cell. Does not reorder enemy storage.
/// </summary>
[BurstCompile]
public struct GridSpatialPartitionSerialBuildJob : IJob
{
    [ReadOnly] public NativeArray<EnemyMotion> Motion;
    public int EnemyCount;

    public float2 BoundsMin;
    public float BoundsMaxX;
    public float BoundsMaxY;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;
    public int TotalCells;

    public NativeArray<int> CellCounts;
    public NativeArray<int> CellStarts;
    public NativeArray<int> WriteOffsets;
    public NativeArray<int> SortedEnemyIndices;
    public NativeArray<int> CellIndexByEnemy;

    public void Execute()
    {
        int N = EnemyCount;

        for (int c = 0; c < TotalCells; c++)
            CellCounts[c] = 0;

        for (int i = 0; i < N; i++)
        {
            int cell = GetCellIndexClamped(
                Motion[i].position,
                BoundsMin,
                BoundsMaxX,
                BoundsMaxY,
                CellSize,
                GridWidth,
                GridHeight);
            CellIndexByEnemy[i] = cell;
            CellCounts[cell]++;
        }

        CellStarts[0] = 0;
        for (int i = 0; i < TotalCells; i++)
            CellStarts[i + 1] = CellStarts[i] + CellCounts[i];

        for (int i = 0; i <= TotalCells; i++)
            WriteOffsets[i] = CellStarts[i];

        for (int i = 0; i < N; i++)
        {
            int cell = CellIndexByEnemy[i];
            int dest = WriteOffsets[cell];
            SortedEnemyIndices[dest] = i;
            WriteOffsets[cell]++;
        }
    }

    static int GetCellIndexClamped(
        float2 position,
        float2 boundsMin,
        float boundsMaxX,
        float boundsMaxY,
        float cellSize,
        int gridWidth,
        int gridHeight)
    {
        float x = math.clamp(position.x, boundsMin.x, boundsMaxX - 0.0001f);
        float y = math.clamp(position.y, boundsMin.y, boundsMaxY - 0.0001f);
        int cx = (int)((x - boundsMin.x) / cellSize);
        int cy = (int)((y - boundsMin.y) / cellSize);
        cx = math.clamp(cx, 0, gridWidth - 1);
        cy = math.clamp(cy, 0, gridHeight - 1);
        return cy * gridWidth + cx;
    }
}
