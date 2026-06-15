using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Randomizes <see cref="Custom/EnemySDFShader"/> material properties into a two-circle humanoid blob:
/// circle 0 = head (smaller, upper), circle 1 = torso (larger, lower), with smooth neck merge and edge noise.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySdfMaterialRandomizer : MonoBehaviour
{
    static readonly int Circle0Center = Shader.PropertyToID("_Circle0Center");
    static readonly int Circle1Center = Shader.PropertyToID("_Circle1Center");
    static readonly int Circle0Radius = Shader.PropertyToID("_Circle0Radius");
    static readonly int Circle1Radius = Shader.PropertyToID("_Circle1Radius");
    static readonly int CircleMerge = Shader.PropertyToID("_CircleMerge");
    static readonly int Circle0Color = Shader.PropertyToID("_Circle0Color");
    static readonly int Circle1Color = Shader.PropertyToID("_Circle1Color");
    static readonly int CircleEdgeNoise = Shader.PropertyToID("_CircleEdgeNoise");
    static readonly int CircleNoiseScale = Shader.PropertyToID("_CircleNoiseScale");
    static readonly int CircleNoiseEdgeWidth = Shader.PropertyToID("_CircleNoiseEdgeWidth");
    static readonly int CircleNoiseSeed = Shader.PropertyToID("_CircleNoiseSeed");
    static readonly int CircleNoiseScrollRate = Shader.PropertyToID("_CircleNoiseScrollRate");

    [SerializeField] Material material;
    [SerializeField] Renderer targetRenderer;
    [SerializeField] bool randomizeOnEnable = true;

    [Header("Head (circle 0)")]
    [SerializeField] FloatRange headRadius = new() { min = 0.12f, max = 0.19f };
    [SerializeField] FloatRange headY = new() { min = 0.22f, max = 0.42f };
    [SerializeField] FloatRange headX = new() { min = -0.07f, max = 0.07f };

    [Header("Torso (circle 1)")]
    [SerializeField] FloatRange bodyRadius = new() { min = 0.20f, max = 0.30f };
    [SerializeField] FloatRange bodyY = new() { min = -0.18f, max = 0.08f };
    [SerializeField] FloatRange bodyXOffsetFromHead = new() { min = -0.06f, max = 0.06f };
    [SerializeField] FloatRange extraBodyRadiusOverHead = new() { min = 0.04f, max = 0.14f };

    [Header("Neck merge")]
    [SerializeField] FloatRange circleMerge = new() { min = 0.07f, max = 0.22f };

    [Header("Edge noise")]
    [SerializeField] FloatRange edgeNoiseAmount = new() { min = 0.012f, max = 0.045f };
    [SerializeField] FloatRange noiseScale = new() { min = 10f, max = 18f };
    [SerializeField] FloatRange noiseEdgeWidth = new() { min = 0.03f, max = 0.08f };
    [Tooltip("Scroll speed through the edge noise field. 0 = frozen.")]
    [SerializeField] float noiseScrollRate;

    [Header("Blood palette")]
    [SerializeField] FloatRange hue = new() { min = 0f, max = 0.07f };
    [SerializeField] FloatRange headValue = new() { min = 0.72f, max = 1f };
    [SerializeField] FloatRange bodyValue = new() { min = 0.38f, max = 0.72f };

    Material _runtimeMaterial;

    void OnEnable()
    {
        ResolveMaterial();
        ApplyNoiseScrollRate();
        if (randomizeOnEnable)
            Randomize();
    }

    void Update() => ApplyNoiseScrollRate();

    void OnValidate() => ApplyNoiseScrollRate();

    void ApplyNoiseScrollRate()
    {
        if (_runtimeMaterial == null)
            ResolveMaterial();
        _runtimeMaterial.SetFloat(CircleNoiseScrollRate, noiseScrollRate);
    }

    void ResolveMaterial()
    {
        if (targetRenderer != null)
            _runtimeMaterial = targetRenderer.material;
        else
            _runtimeMaterial = material;
    }

    [ContextMenu("Randomize")]
    public void Randomize()
    {
        if (_runtimeMaterial == null)
            ResolveMaterial();

        var rng = Unity.Mathematics.Random.CreateFromIndex((uint)Random.Range(1, int.MaxValue));

        float headR = headRadius.ResolveUniform(ref rng);
        float bodyR = math.max(bodyRadius.ResolveUniform(ref rng), headR + extraBodyRadiusOverHead.ResolveUniform(ref rng));
        float headCenterX = headX.ResolveUniform(ref rng);
        float headCenterY = headY.ResolveUniform(ref rng);
        float bodyCenterX = headCenterX + bodyXOffsetFromHead.ResolveUniform(ref rng);
        float bodyCenterY = bodyY.ResolveUniform(ref rng);

        if (bodyCenterY > headCenterY - headR * 0.35f)
            bodyCenterY = headCenterY - headR * rng.NextFloat(0.45f, 0.85f);

        _runtimeMaterial.SetVector(Circle0Center, new Vector4(headCenterX, headCenterY, 0f, 0f));
        _runtimeMaterial.SetVector(Circle1Center, new Vector4(bodyCenterX, bodyCenterY, 0f, 0f));
        _runtimeMaterial.SetFloat(Circle0Radius, headR);
        _runtimeMaterial.SetFloat(Circle1Radius, bodyR);
        _runtimeMaterial.SetFloat(CircleMerge, circleMerge.ResolveUniform(ref rng));

        RandomizeBloodColors(ref rng, out Color headColor, out Color bodyColor);
        _runtimeMaterial.SetColor(Circle0Color, headColor);
        _runtimeMaterial.SetColor(Circle1Color, bodyColor);

        _runtimeMaterial.SetFloat(CircleEdgeNoise, edgeNoiseAmount.ResolveUniform(ref rng));
        _runtimeMaterial.SetFloat(CircleNoiseScale, noiseScale.ResolveUniform(ref rng));
        _runtimeMaterial.SetFloat(CircleNoiseEdgeWidth, noiseEdgeWidth.ResolveUniform(ref rng));
        _runtimeMaterial.SetFloat(CircleNoiseSeed, rng.NextFloat(0f, 1000f));
    }

    void RandomizeBloodColors(ref Unity.Mathematics.Random rng, out Color headColor, out Color bodyColor)
    {
        float h = hue.ResolveUniform(ref rng);
        headColor = Color.HSVToRGB(h, rng.NextFloat(0.35f, 0.75f), headValue.ResolveUniform(ref rng));
        headColor.a = 1f;
        bodyColor = Color.HSVToRGB(
            h + rng.NextFloat(-0.015f, 0.015f),
            rng.NextFloat(0.45f, 0.9f),
            bodyValue.ResolveUniform(ref rng));
        bodyColor.a = 1f;
    }
}
