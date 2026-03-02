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
    /// <summary>Digits packed into 4 bits each (nibble 0 = leftmost digit). Max 8 digits (32 bits).</summary>
    public uint packedDigits;
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

    public void Spawn(float2 position, int damageValue, float lifetime = DefaultLifetime)
    {
        if (damageValue <= 0) return;

        int digits = CountDigits(damageValue);
        _numbers.Add(new DamageNumber
        {
            position = position,
            velocity = new float2(0f, DefaultRiseSpeed),
            timeAlive = 0f,
            lifetime = lifetime,
            opacity = 1f,
            damageValue = damageValue,
            digitCount = digits,
            packedDigits = PackDigits(damageValue, digits)
        });
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
