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

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy e = enemies[i];
            if (e.visual.frameIndex < 0 || e.visual.frameIndex >= frames.Length) continue;

            SpriteFrame f = frames[e.visual.frameIndex];
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
            if (a.visual.frameIndex < 0 || a.visual.frameIndex >= frames.Length) continue;

            SpriteFrame f = frames[a.visual.frameIndex];
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
                if (es.visual.frameIndex < 0 || es.visual.frameIndex >= frames.Length) continue;

                SpriteFrame f = frames[es.visual.frameIndex];
                _buffer[_count++] = new SpriteInstanceData
                {
                    position = new float3(es.position, 0f),
                    scale = es.visual.scale,
                    uvRect = new float4(f.uvMin, f.uvMax)
                };
            }
        }
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _buffer.Length) return;
        int newCap = needed;
        if (newCap < _buffer.Length * 2) newCap = _buffer.Length * 2;
        _buffer = new SpriteInstanceData[newCap];
    }
}
