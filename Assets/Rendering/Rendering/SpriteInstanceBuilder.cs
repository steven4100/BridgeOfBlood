using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Builds a combined SpriteInstanceData[] from enemy and attack entity arrays.
/// Entities with frameIndex &lt; 0 (no baked visual) are skipped.
/// Reuses a single managed array to avoid per-frame allocations.
/// </summary>
public class SpriteInstanceBuilder
{
    private static readonly float4 FrozenAdditiveRgb = new float4(0f, 0.06f, 0.14f, 0f);
    private static readonly float4 IgniteFlashAdditiveRgb = new float4(0.22f, 0.09f, 0.02f, 0f);
    private static readonly float4 PoisonFlashAdditiveRgb = new float4(0.03f, 0.20f, 0.05f, 0f);
    private static readonly float4 BleedFlashAdditiveRgb = new float4(0.20f, 0.03f, 0.03f, 0f);

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

    public void Build(EnemyBuffers enemies, NativeArray<AttackEntity> attacks, NativeArray<EffectSprite> effectSprites = default)
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
            EnemyMotion m = enemies.Motion[i];
            EnemyPresentation pr = enemies.Presentation[i];
            StatusAilmentFlag st = enemies.Status[i];
            int dbIndex = ResolveDatabaseFrameIndex(in pr.visual, pr.visualTime, dbLen);
            if (dbIndex < 0) continue;

            SpriteFrame f = frames[dbIndex];
            _buffer[_count++] = new SpriteInstanceData
            {
                positionScale = new float4(m.position.x, m.position.y, 0f, pr.visual.scale),
                uvRect = new float4(f.uvMin, f.uvMax),
                color = ComputeEnemyTint(st, in pr)
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
                positionScale = new float4(a.position.x, a.position.y, 0f, a.visual.scale),
                uvRect = new float4(f.uvMin, f.uvMax),
                color = default
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
                    positionScale = new float4(es.position.x, es.position.y, 0f, es.visual.scale),
                    uvRect = new float4(f.uvMin, f.uvMax),
                    color = default
                };
            }
        }
    }

    private static float4 ComputeEnemyTint(StatusAilmentFlag status, in EnemyPresentation p)
    {
        float4 add = default;
        if ((status & StatusAilmentFlag.Frozen) != 0)
            add += FrozenAdditiveRgb;
        if (p.ailmentFlashTimer > 0f)
        {
            float k = math.saturate(p.ailmentFlashTimer / TickDamagePipeline.DotFlashDurationSeconds);
            add += DotFlashAdditiveRgb(p.ailmentFlashSource) * k;
        }

        return add;
    }

    private static float4 DotFlashAdditiveRgb(TickDamageSource source)
    {
        switch (source)
        {
            case TickDamageSource.Fire:
                return IgniteFlashAdditiveRgb;
            case TickDamageSource.Poison:
                return PoisonFlashAdditiveRgb;
            case TickDamageSource.Bleed:
                return BleedFlashAdditiveRgb;
            default:
                return default;
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
