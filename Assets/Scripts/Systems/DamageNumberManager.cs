using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// A live damage number with position, motion, fade, and the integer value to display.
/// Blittable for NativeList storage.
/// </summary>
public struct DamageNumber
{
    public float2 position;
    public float2 velocity;
    public float timeAlive;
    public float lifetime;
    public float opacity;
    public int damageValue;
    public int digitCount;
    /// <summary>Digits packed into 4 bits each (nibble 0 = leftmost digit). Value 10 = exclamation (crit). Max 8 digits (32 bits).</summary>
    public uint packedDigits;
    /// <summary>Visual scale (larger damage = larger number). Applied in render.</summary>
    public float scale;
    /// <summary>If true, render in yellow and show trailing '!'.</summary>
    public bool isCrit;
}

/// <summary>
/// Manages live damage number entities. Spawns from hit events, updates motion/opacity each frame,
/// removes expired entries. NativeList-backed, no MonoBehaviours.
/// </summary>
public class DamageNumberManager
{
    private NativeList<DamageNumber> _numbers;

    private const float DefaultLifetime = 0.8f;
    private const float DefaultRiseSpeed = 40f;

    public DamageNumberManager()
    {
        _numbers = new NativeList<DamageNumber>(256, Allocator.Persistent);
    }

    /// <param name="velocityX">Horizontal speed (e.g. enemy.moveSpeed) so the number moves right with the target. 0 = no horizontal drift.</param>
    /// <param name="isCrit">If true, number is shown in yellow with a trailing '!'.</param>
    public void Spawn(float2 position, int damageValue, float lifetime = DefaultLifetime, float velocityX = 0f, bool isCrit = false)
    {
        if (damageValue <= 0) return;

        int digits = CountDigits(damageValue);
        uint packed = PackDigits(damageValue, digits);
        int digitCount = digits;
        if (isCrit)
        {
            digitCount = digits + 1;
            packed |= 10u << (digits * 4);
        }

        float scale = ScaleFromDamage(damageValue);

        _numbers.Add(new DamageNumber
        {
            position = position,
            velocity = new float2(velocityX, DefaultRiseSpeed),
            timeAlive = 0f,
            lifetime = lifetime,
            opacity = 1f,
            damageValue = damageValue,
            digitCount = digitCount,
            packedDigits = packed,
            scale = scale,
            isCrit = isCrit
        });
    }

    /// <summary>Larger damage values get a larger scale (e.g. 100 -> ~1.5, 1000 -> ~1.75).</summary>
    static float ScaleFromDamage(int damageValue)
    {
        if (damageValue <= 0) return 1f;
        float log = math.log10(math.max(1, damageValue));
        return math.clamp(1f + 0.25f * log, 1f, 2.5f);
    }

    public void Update(float dt)
    {
        for (int i = _numbers.Length - 1; i >= 0; i--)
        {
            DamageNumber n = _numbers[i];
            n.timeAlive += dt;

            if (n.timeAlive >= n.lifetime)
            {
                _numbers.RemoveAtSwapBack(i);
                continue;
            }

            float t = n.timeAlive / n.lifetime;
            n.position += n.velocity * dt;
            n.opacity = 1f - t * t;
            _numbers[i] = n;
        }
    }

    public NativeArray<DamageNumber> GetEntities() => _numbers.AsArray();
    public int Count => _numbers.Length;

    public void Clear() => _numbers.Clear();

    public void Dispose()
    {
        if (_numbers.IsCreated)
            _numbers.Dispose();
    }

    static int CountDigits(int value)
    {
        if (value <= 0) return 1;
        int count = 0;
        while (value > 0) { count++; value /= 10; }
        return count;
    }

    /// <summary>
    /// Packs digits left-to-right into nibbles: nibble 0 = most significant digit.
    /// E.g. 1234 -> nibble0=1, nibble1=2, nibble2=3, nibble3=4.
    /// </summary>
    static uint PackDigits(int value, int digitCount)
    {
        uint packed = 0;
        for (int i = digitCount - 1; i >= 0; i--)
        {
            int digit = value % 10;
            value /= 10;
            packed |= (uint)digit << (i * 4);
        }
        return packed;
    }
}
