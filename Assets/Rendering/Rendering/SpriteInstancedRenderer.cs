using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders sprites via a texture atlas using Graphics.RenderMeshIndirect.
/// Uploads SpriteInstanceData per instance to a StructuredBuffer; the shader handles
/// per-instance UV remapping from atlas coordinates.
/// Single draw call for all sprites regardless of count.
/// </summary>
public class SpriteInstancedRenderer
{
    private const int InitialCapacity = 1024;

    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly MaterialPropertyBlock _mpb;

    private GraphicsBuffer _instanceBuffer;
    private GraphicsBuffer _argsBuffer;
    private SpriteInstanceData[] _cpuData;
    private int _bufferCapacity;

    private static readonly int PropInstanceData = Shader.PropertyToID("_InstanceData");
    private static readonly int PropLocalToWorld = Shader.PropertyToID("_LocalToWorld");

    public float DepthOffsetTowardCamera { get; set; } = 0.05f;

    public SpriteInstancedRenderer(Material material)
    {
        _mesh = CreateQuadMesh();

        if (material != null)
            _material = new Material(material);
        else
            _material = CreateDefaultMaterial();

        _mpb = new MaterialPropertyBlock();

        _bufferCapacity = InitialCapacity;
        _cpuData = new SpriteInstanceData[_bufferCapacity];
        _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _bufferCapacity,
            UnsafeUtility.SizeOf<SpriteInstanceData>());
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
    }

    public void Render(SpriteInstanceData[] data, int count, RectTransform rectTransform, Camera camera)
    {
        if (rectTransform == null || count == 0 || camera == null) return;

        EnsureCapacity(count);

        System.Array.Copy(data, _cpuData, count);
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
            UnsafeUtility.SizeOf<SpriteInstanceData>());
        _cpuData = new SpriteInstanceData[newCap];
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
        var mesh = new Mesh { name = "SpriteQuad" };
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
        var shader = Shader.Find("BridgeOfBlood/InstancedSprite");
        if (shader == null)
        {
            Debug.LogError("SpriteInstancedRenderer: Shader 'BridgeOfBlood/InstancedSprite' not found.");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        return new Material(shader);
    }
}
