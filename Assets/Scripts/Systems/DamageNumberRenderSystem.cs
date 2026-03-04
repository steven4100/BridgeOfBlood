using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU instance data for one damage number. Must match the shader's InstanceData struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DamageNumberInstanceData
{
    public Vector2 localPos;
    public float opacity;
    public uint packedDigits;
    public int digitCount;
    public float scale;
    public Vector3 color;
}

/// <summary>
/// Renders damage numbers using Graphics.RenderMeshIndirect (Unity 6).
/// One instanced quad per number; the shader unpacks digits from packedDigits and samples a digit atlas.
/// Single draw call for all damage numbers regardless of count.
/// </summary>
public class DamageNumberRenderSystem
{
    private const int InitialCapacity = 256;

    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly MaterialPropertyBlock _mpb;

    private GraphicsBuffer _instanceBuffer;
    private GraphicsBuffer _argsBuffer;
    private DamageNumberInstanceData[] _cpuData;
    private int _bufferCapacity;

    private static readonly int PropInstanceData = Shader.PropertyToID("_InstanceData");
    private static readonly int PropLocalToWorld = Shader.PropertyToID("_LocalToWorld");
    private static readonly int PropDigitScale = Shader.PropertyToID("_DigitScale");

    /// <summary>Height of each digit in rect-local units.</summary>
    public float DigitScale { get; set; } = 8f;

    public float DepthOffsetTowardCamera { get; set; } = 0.2f;

    public DamageNumberRenderSystem(Material material)
    {
        _mesh = CreateQuadMesh();

        if (material != null)
            _material = new Material(material);
        else
            _material = CreateDefaultMaterial();

        _mpb = new MaterialPropertyBlock();

        _bufferCapacity = InitialCapacity;
        _cpuData = new DamageNumberInstanceData[_bufferCapacity];
        _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _bufferCapacity,
            UnsafeUtility.SizeOf<DamageNumberInstanceData>());
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
    }

    static readonly Vector3 CritColor = new Vector3(1f, 1f, 0f);
    static readonly Vector3 NormalColor = new Vector3(1f, 1f, 1f);


    public void Render(NativeArray<DamageNumber> numbers, RectTransform rectTransform, Camera camera)
    {
        if (rectTransform == null || numbers.Length == 0 || camera == null) return;

        int count = numbers.Length;
        EnsureCapacity(count);


        for (int i = 0; i < count; i++)
        {
            DamageNumber n = numbers[i];
            _cpuData[i] = new DamageNumberInstanceData
            {
                localPos = n.position,
                opacity = n.opacity,
                packedDigits = n.packedDigits,
                digitCount = n.digitCount,
                scale = n.scale > 0f ? n.scale : 1f,
                color = n.isCrit ? CritColor : NormalColor
            };
        }

        _instanceBuffer.SetData(_cpuData, 0, 0, count);

        var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        args[0].indexCountPerInstance = _mesh.GetIndexCount(0);
        args[0].instanceCount = (uint)count;
        args[0].startIndex = _mesh.GetIndexStart(0);
        args[0].baseVertexIndex = _mesh.GetBaseVertex(0);
        args[0].startInstance = 0;
        _argsBuffer.SetData(args);

        Matrix4x4 localToWorld = rectTransform.localToWorldMatrix;
        Vector3 forward = rectTransform.forward;
        localToWorld *= Matrix4x4.Translate(-forward * DepthOffsetTowardCamera);

        _mpb.SetBuffer(PropInstanceData, _instanceBuffer);
        _mpb.SetMatrix(PropLocalToWorld, localToWorld);
        _mpb.SetFloat(PropDigitScale, DigitScale);

        RenderParams rp = new RenderParams(_material)
        {
            worldBounds = ComputeWorldBounds(rectTransform),
            matProps = _mpb,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            camera = camera
        };

        Graphics.RenderMeshIndirect(in rp, _mesh, _argsBuffer, 1, 0);
    }

    public void Dispose()
    {
        _instanceBuffer?.Dispose();
        _argsBuffer?.Dispose();
        _instanceBuffer = null;
        _argsBuffer = null;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _bufferCapacity) return;

        int newCap = Mathf.NextPowerOfTwo(needed);
        _instanceBuffer?.Dispose();
        _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, newCap,
            UnsafeUtility.SizeOf<DamageNumberInstanceData>());
        _cpuData = new DamageNumberInstanceData[newCap];
        _bufferCapacity = newCap;
    }

    private static Bounds ComputeWorldBounds(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Bounds b = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++)
            b.Encapsulate(corners[i]);
        b.Expand(50f);
        return b;
    }

    /// <summary>
    /// Unit quad (0,0)-(1,1) with UVs. The vertex shader stretches it horizontally by digitCount.
    /// </summary>
    private static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "DamageNumberQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateDefaultMaterial()
    {
        var shader = Shader.Find("BridgeOfBlood/DamageNumberUnlit");
        if (shader == null)
        {
            Debug.LogError("DamageNumberRenderSystem: Shader 'BridgeOfBlood/DamageNumberUnlit' not found.");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        var mat = new Material(shader);
        mat.SetTexture("_DigitAtlas", GenerateDigitAtlas());
        return mat;
    }

    /// <summary>
    /// Generates a 352x32 digit atlas texture at runtime (white digits on transparent black).
    /// 11 cells: 0-9 and exclamation (for crits). Each cell is 32x32 pixels.
    /// </summary>
    public static Texture2D GenerateDigitAtlas()
    {
        const int cellSize = 32;
        const int cellCount = 11;
        const int width = cellSize * cellCount;
        const int height = cellSize;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        bool[][] patterns = GetDigitPatterns();
        const int gridW = 3;
        const int gridH = 5;
        int pixW = cellSize / (gridW + 2);
        int pixH = cellSize / (gridH + 2);
        int padX = (cellSize - gridW * pixW) / 2;
        int padY = (cellSize - gridH * pixH) / 2;

        for (int d = 0; d < patterns.Length; d++)
        {
            int cellX = d * cellSize;
            bool[] pat = patterns[d];
            for (int gy = 0; gy < gridH; gy++)
            {
                for (int gx = 0; gx < gridW; gx++)
                {
                    if (!pat[gy * gridW + gx]) continue;
                    int baseX = cellX + padX + gx * pixW;
                    int baseY = padY + (gridH - 1 - gy) * pixH;
                    for (int py = 0; py < pixH; py++)
                        for (int px = 0; px < pixW; px++)
                            pixels[(baseY + py) * width + baseX + px] = new Color32(255, 255, 255, 255);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>
    /// 3x5 pixel font patterns for digits 0-9 and exclamation (index 10).
    /// </summary>
    static bool[][] GetDigitPatterns()
    {
        return new[]
        {
            ToBools("111101101101111"), // 0
            ToBools("010110010010111"), // 1
            ToBools("111001111100111"), // 2
            ToBools("111001111001111"), // 3
            ToBools("101101111001001"), // 4
            ToBools("111100111001111"), // 5
            ToBools("111100111101111"), // 6
            ToBools("111001001001001"), // 7
            ToBools("111101111101111"), // 8
            ToBools("111101111001111"), // 9
            ToBools("010010010000010"), // 10 = !
        };
    }

    static bool[] ToBools(string s)
    {
        var b = new bool[s.Length];
        for (int i = 0; i < s.Length; i++)
            b[i] = s[i] == '1';
        return b;
    }
}
