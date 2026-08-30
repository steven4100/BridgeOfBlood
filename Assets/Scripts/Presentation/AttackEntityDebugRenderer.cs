using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders attack entity hitboxes into the Game view using Graphics.DrawMesh.
/// Also implements IDebugDrawable to draw Gizmo spheres at entity positions in the Scene view.
/// Plain class, no MonoBehaviour. Driven by <see cref="CombatPresentationLayer"/>.
/// </summary>
public class AttackEntityDebugRenderer : IDebugDrawable
{
	private const int CircleSegments = 24;

	private AttackEntityManager _manager;
	private readonly Mesh _circleMesh;
	private readonly Mesh _quadMesh;
	private readonly Material _material;

	public AttackEntityDebugRenderer(Material material = null)
	{
		_circleMesh = CreateCircleMesh();
		_quadMesh = CreateQuadMesh();
		_material = material != null ? material : CreateMaterial();
	}

	public void Bind(AttackEntityManager manager)
	{
		_manager = manager;
	}

	public void Render(NativeArray<AttackEntity> entities, NativeArray<HitBoxRuntime> hitBoxes, RectTransform simZone, Camera camera)
	{
		if (entities.Length == 0 || simZone == null || camera == null) return;

		Matrix4x4 localToWorld = simZone.localToWorldMatrix;
		Vector3 forward = simZone.forward;
		localToWorld *= Matrix4x4.Translate(-forward * 0.05f);
		try
		{
			for (int i = 0; i < entities.Length; i++)
			{
				AttackEntity e = entities[i];
				if (!e.entityId.IsValid)
					continue;

				HitBoxRuntime hitBox = hitBoxes[i];
				float scale = hitBox.currentScale;

				Mesh mesh;
				float sizeX, sizeY;

				if (hitBox.isActive && hitBox.hitBox.isSphere)
				{
					mesh = _circleMesh;
					float diameter = hitBox.hitBox.sphereRadius * scale * 2f;
					sizeX = diameter;
					sizeY = diameter;
				}
				else if (hitBox.isActive && hitBox.hitBox.isRect)
				{
					mesh = _quadMesh;
					sizeX = hitBox.hitBox.rectDimension.x * scale;
					sizeY = hitBox.hitBox.rectDimension.y * scale;
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
		catch
		{
			Debug.LogError("oops");
		}
	}

	public void DrawGizmos(Transform transform)
	{
		if (_manager == null || transform == null || _manager.EntityCount == 0) return;

		try
		{
			NativeArray<AttackEntity> entities = _manager.GetEntities();
			NativeArray<HitBoxRuntime> hitBoxes = _manager.GetHitBoxes();
			for (int i = 0; i < entities.Length; i++)
			{
				AttackEntity e = entities[i];
				if (!e.entityId.IsValid)
					continue;

				HitBoxRuntime hitBox = hitBoxes[i];
				float scale = hitBox.currentScale;

				Vector3 worldPos = transform.TransformPoint(new Vector3(e.position.x, e.position.y, 0f));
				float worldScale = transform.lossyScale.x;

				float radius;
				if (hitBox.isActive && hitBox.hitBox.isSphere)
					radius = hitBox.hitBox.sphereRadius * scale * worldScale;
				else if (hitBox.isActive && hitBox.hitBox.isRect)
					radius = math.length(hitBox.hitBox.rectDimension * 0.5f) * scale * worldScale;
				else
					radius = 2f * worldScale;

				Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
				Gizmos.DrawWireSphere(worldPos, radius);
				Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
				Gizmos.DrawSphere(worldPos, radius);
			}
		}
		catch
		{
			Debug.LogError("oops");
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
