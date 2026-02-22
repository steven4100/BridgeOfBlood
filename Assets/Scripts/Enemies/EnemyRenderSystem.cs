using System.Runtime.InteropServices;
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU instance data uploaded each frame. Must match the shader's InstanceData struct layout.
/// Only the local position relative to the RectTransform is shipped to the GPU.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EnemyInstanceData
{
    public Vector2 localPos;
}

/// <summary>
/// Renders enemies using Graphics.RenderMeshIndirect (Unity 6).
/// Uploads only float2 localPos per instance to a StructuredBuffer; the shader handles
/// local-to-world transform, depth offset, and billboarding.
/// One draw call for all enemies regardless of count.
/// </summary>
public class EnemyRenderSystem
{
    private static readonly Color DefaultColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    private const int InitialCapacity = 1024;

    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly MaterialPropertyBlock _mpb;

    private GraphicsBuffer _instanceBuffer;
    private GraphicsBuffer _argsBuffer;
    private EnemyInstanceData[] _cpuData;
    private int _bufferCapacity;

    private static readonly int PropInstanceData = Shader.PropertyToID("_InstanceData");
    private static readonly int PropLocalToWorld = Shader.PropertyToID("_LocalToWorld");
    private static readonly int PropInstanceScale = Shader.PropertyToID("_InstanceScale");
    private static readonly int PropColor = Shader.PropertyToID("_Color");

    public float DepthOffsetTowardCamera { get; set; } = 0.1f;
    public float InstanceScale { get; set; } = 10f;

    public EnemyRenderSystem(Material material = null, float instanceScale = 10f)
    {
        _mesh = CreateQuadMesh();
        InstanceScale = instanceScale;

        if (material != null)
            _material = new Material(material);
        else
            _material = CreateDefaultMaterial();

        _mpb = new MaterialPropertyBlock();

        _bufferCapacity = InitialCapacity;
        _cpuData = new EnemyInstanceData[_bufferCapacity];
        _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _bufferCapacity,
            UnsafeUtility.SizeOf<EnemyInstanceData>());

        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
    }

    public void RenderEnemies(NativeArray<Enemy> enemies, RectTransform rectTransform, Camera camera)
    {
        if (rectTransform == null || enemies.Length == 0 || camera == null) return;

        int count = enemies.Length;
        EnsureCapacity(count);

        for (int i = 0; i < count; i++)
            _cpuData[i].localPos = enemies[i].position;

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
        _mpb.SetFloat(PropInstanceScale, InstanceScale);
        _mpb.SetColor(PropColor, DefaultColor);

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
            UnsafeUtility.SizeOf<EnemyInstanceData>());
        _cpuData = new EnemyInstanceData[newCap];
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

    private static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "EnemyQuad" };
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
        var shader = Shader.Find("BridgeOfBlood/EnemyIndirectUnlit");
        if (shader == null)
        {
            Debug.LogError("EnemyRenderSystem: Shader 'BridgeOfBlood/EnemyIndirectUnlit' not found.");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        var mat = new Material(shader);
        mat.color = DefaultColor;
        return mat;
    }
}
