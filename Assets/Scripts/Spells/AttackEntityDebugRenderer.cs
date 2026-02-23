using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders attack entity hitboxes into the Game view using Graphics.DrawMesh.
/// Also implements IDebugDrawable to draw Gizmo spheres at entity positions in the Scene view.
/// Plain class, no MonoBehaviour. Call Render() from the game loop after entity updates.
/// </summary>
public class AttackEntityDebugRenderer : IDebugDrawable
{
    private const int CircleSegments = 24;

    private readonly AttackEntityManager _manager;
    private readonly Mesh _circleMesh;
    private readonly Mesh _quadMesh;
    private readonly Material _material;

    public AttackEntityDebugRenderer(AttackEntityManager manager, Material material = null)
    {
        _manager = manager;
        _circleMesh = CreateCircleMesh();
        _quadMesh = CreateQuadMesh();
        _material = material != null ? material : CreateMaterial();
    }

    public void Render(NativeArray<AttackEntity> entities, RectTransform simZone, Camera camera)
    {
        if (entities.Length == 0 || simZone == null || camera == null) return;

        Matrix4x4 localToWorld = simZone.localToWorldMatrix;
        Vector3 forward = simZone.forward;
        localToWorld *= Matrix4x4.Translate(-forward * 0.05f);

        for (int i = 0; i < entities.Length; i++)
        {
            AttackEntity e = entities[i];
            float scale = e.currentHitBoxScale;

            Mesh mesh;
            float sizeX, sizeY;

            if (e.hitBox.isSphere)
            {
                mesh = _circleMesh;
                float diameter = e.hitBox.sphereRadius * scale * 2f;
                sizeX = diameter;
                sizeY = diameter;
            }
            else if (e.hitBox.isRect)
            {
                mesh = _quadMesh;
                sizeX = e.hitBox.rectDimension.x * scale;
                sizeY = e.hitBox.rectDimension.y * scale;
            }
            else
            {
                mesh = _circleMesh;
                sizeX = 4f;
                sizeY = 4f;
            }

            Matrix4x4 entityMatrix = localToWorld
                * Matrix4x4.TRS(
                    new Vector3(e.position.x, e.position.y, 0f),
                    Quaternion.identity,
                    new Vector3(sizeX, sizeY, 1f));

            Graphics.DrawMesh(mesh, entityMatrix, _material, 0, camera, 0, null,
                ShadowCastingMode.Off, false);
        }
    }

    public void DrawGizmos(Transform transform)
    {
        if (_manager == null || transform == null || _manager.EntityCount == 0) return;

        NativeArray<AttackEntity> entities = _manager.GetEntities();
        for (int i = 0; i < entities.Length; i++)
        {
            AttackEntity e = entities[i];
            float scale = e.currentHitBoxScale;

            Vector3 worldPos = transform.TransformPoint(new Vector3(e.position.x, e.position.y, 0f));
            float worldScale = transform.lossyScale.x;

            float radius;
            if (e.hitBox.isSphere)
                radius = e.hitBox.sphereRadius * scale * worldScale;
            else if (e.hitBox.isRect)
                radius = math.length(e.hitBox.rectDimension * 0.5f) * scale * worldScale;
            else
                radius = 2f * worldScale;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
            Gizmos.DrawWireSphere(worldPos, radius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawSphere(worldPos, radius);
        }
    }

    public void Dispose()
    {
        if (_circleMesh != null) Object.Destroy(_circleMesh);
        if (_quadMesh != null) Object.Destroy(_quadMesh);
    }

    static Mesh CreateCircleMesh()
    {
        var mesh = new Mesh { name = "DebugCircle" };
        int vertCount = CircleSegments + 1;
        var verts = new Vector3[vertCount];
        var tris = new int[CircleSegments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
        }

        for (int i = 0; i < CircleSegments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % CircleSegments + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "DebugQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material CreateMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        var mat = new Material(shader);
        mat.color = Color.white;
        return mat;
    }
}
