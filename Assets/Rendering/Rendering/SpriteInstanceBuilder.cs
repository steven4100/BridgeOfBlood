using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Builds a combined SpriteInstanceData[] from enemy and attack entity arrays.
/// Entities with frameIndex &lt; 0 (no baked visual) are skipped.
/// Reuses a single managed array to avoid per-frame allocations.
/// </summary>
public class SpriteInstanceBuilder
{
    private readonly SpriteRenderDatabase _database;
    private SpriteInstanceData[] _buffer;
    private int _count;

    public SpriteInstanceBuilder(SpriteRenderDatabase database, int initialCapacity = 2048)
    {
        _database = database;
        _buffer = new SpriteInstanceData[initialCapacity];
    }

    public SpriteInstanceData[] Buffer => _buffer;
    public int Count => _count;

    public void Build(NativeArray<Enemy> enemies, NativeArray<AttackEntity> attacks, NativeArray<EffectSprite> effectSprites = default)
    {
        _count = 0;

        if (_database == null || _database.frames == null || _database.frames.Length == 0)
            return;

        int maxNeeded = enemies.Length + attacks.Length + (effectSprites.IsCreated ? effectSprites.Length : 0);
        EnsureCapacity(maxNeeded);

        SpriteFrame[] frames = _database.frames;
        int dbLen = frames.Length;

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy e = enemies[i];
            int dbIndex = ResolveDatabaseFrameIndex(in e.visual, e.visualTime, dbLen);
            if (dbIndex < 0) continue;

            SpriteFrame f = frames[dbIndex];
            _buffer[_count++] = new SpriteInstanceData
            {
                position = new float3(e.position, 0f),
                scale = e.visual.scale,
                uvRect = new float4(f.uvMin, f.uvMax)
            };
        }

        for (int i = 0; i < attacks.Length; i++)
        {
            AttackEntity a = attacks[i];
            int dbIndex = ResolveDatabaseFrameIndex(in a.visual, a.timeAlive, dbLen);
            if (dbIndex < 0) continue;

            SpriteFrame f = frames[dbIndex];
            _buffer[_count++] = new SpriteInstanceData
            {
                position = new float3(a.position, 0f),
                scale = a.visual.scale,
                uvRect = new float4(f.uvMin, f.uvMax)
            };
        }

        if (effectSprites.IsCreated)
        {
            for (int i = 0; i < effectSprites.Length; i++)
            {
                EffectSprite es = effectSprites[i];
                int dbIndex = ResolveDatabaseFrameIndex(in es.visual, es.timeAlive, dbLen);
                if (dbIndex < 0) continue;

                SpriteFrame f = frames[dbIndex];
                _buffer[_count++] = new SpriteInstanceData
                {
                    position = new float3(es.position, 0f),
                    scale = es.visual.scale,
                    uvRect = new float4(f.uvMin, f.uvMax)
                };
            }
        }
    }

    private static int ResolveDatabaseFrameIndex(in EntityVisual visual, float elapsedSeconds, int frameDatabaseLength)
    {
        if (visual.frameIndex < 0) return -1;

        int animCount = visual.animationFrameCount <= 1 ? 1 : visual.animationFrameCount;
        int dbIndex;
        if (animCount <= 1)
        {
            dbIndex = visual.frameIndex;
        }
        else
        {
            int local = (int)math.floor(elapsedSeconds * visual.animationFramesPerSecond);
            local %= animCount;
            if (local < 0)
                local += animCount;
            dbIndex = visual.frameIndex + local;
        }

        if (dbIndex < 0 || dbIndex >= frameDatabaseLength) return -1;
        return dbIndex;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _buffer.Length) return;
        int newCap = needed;
        if (newCap < _buffer.Length * 2) newCap = _buffer.Length * 2;
        _buffer = new SpriteInstanceData[newCap];
    }
}
