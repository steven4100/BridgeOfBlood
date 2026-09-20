using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Leech : MonoBehaviour
{
	Chain segmentChain;
	LeechMeshGenerator meshGenerator;
	Mesh leechMesh;

	public List<float> chainSegments = new List<float>();

	[SerializeField] LeechMeshGenerator.LeechMeshConfigData meshConfig;
	[SerializeField] int defaultSegmentCount = 8;
	[SerializeField] float defaultSegmentLength = 0.5f;
	[SerializeField, Range(1f, 180f)] float maxBendDegrees = 35f;
	[SerializeField] float planeZ;
	[SerializeField] bool drawDebugChain;
	[SerializeField] Color debugColor = new Color(0.85f, 0.15f, 0.2f, 1f);
	[SerializeField] float debugPointRadius = 0.08f;

	void Reset()
	{
		meshConfig = LeechMeshGenerator.LeechMeshConfigData.Default();
	}

	void Start()
	{
		if (meshConfig.width == null || meshConfig.height == null)
			meshConfig = LeechMeshGenerator.LeechMeshConfigData.Default();

		EnsureSegments();
		segmentChain = new Chain(chainSegments, GetTargetPositionOnPlane(), maxBendDegrees);
		meshGenerator = new LeechMeshGenerator();

		leechMesh = new Mesh { name = "LeechMesh" };
		leechMesh.MarkDynamic();
		GetComponent<MeshFilter>().mesh = leechMesh;

		MeshRenderer renderer = GetComponent<MeshRenderer>();
		if (renderer.sharedMaterial == null)
			renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

		RebuildMesh();
	}

	void Update()
	{
		segmentChain.MaxBendDegrees = maxBendDegrees;
		segmentChain.SetChainRootPosition(GetTargetPositionOnPlane());
		RebuildMesh();
	}

	void OnDestroy()
	{
		if (leechMesh != null)
			Destroy(leechMesh);
	}

	void EnsureSegments()
	{
		if (chainSegments.Count > 0)
			return;

		for (int i = 0; i < defaultSegmentCount; i++)
			chainSegments.Add(defaultSegmentLength);
	}

	void RebuildMesh()
	{
		meshGenerator.Generate(leechMesh, segmentChain.Positions, planeZ, transform, meshConfig);
	}

	Vector2 GetTargetPositionOnPlane()
	{
		Camera cam = Camera.main;
		Ray ray = cam.ScreenPointToRay(Input.mousePosition);
		var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
		if (plane.Raycast(ray, out float enter))
		{
			Vector3 hit = ray.GetPoint(enter);
			return new Vector2(hit.x, hit.y);
		}

		return transform.position;
	}

	void OnDrawGizmos()
	{
		if (!drawDebugChain || segmentChain == null)
			return;

		Vector2[] positions = segmentChain.Positions;
		Gizmos.color = debugColor;

		for (int i = 0; i < positions.Length; i++)
		{
			Vector3 point = ToWorld(positions[i]);
			Gizmos.DrawSphere(point, debugPointRadius);

			if (i > 0)
				Gizmos.DrawLine(ToWorld(positions[i - 1]), point);
		}
	}

	Vector3 ToWorld(Vector2 point) => new Vector3(point.x, point.y, planeZ);
}

public class LeechMeshGenerator
{
	[System.Serializable]
	public struct LeechMeshConfigData
	{
		public List<NoiseLayer> height;
		public List<NoiseLayer> width;
		[Min(0f)] public float mouthSize;
		[Min(0.01f)] public float baseWidth;
		[Min(0.01f)] public float baseHeight;
		[Range(3, 32)] public int radialSegments;
		[Range(0.01f, 0.5f)] public float mouthBlendLength;
		[Range(0.01f, 0.5f)] public float tailTaperLength;
		public uint noiseSeed;

		public static LeechMeshConfigData Default()
		{
			return new LeechMeshConfigData
			{
				height = new List<NoiseLayer> { NoiseLayer.Default(NoiseType.Perlin) },
				width = new List<NoiseLayer> { NoiseLayer.Default(NoiseType.Perlin) },
				mouthSize = 0.15f,
				baseWidth = 0.35f,
				baseHeight = 0.22f,
				radialSegments = 10,
				mouthBlendLength = 0.12f,
				tailTaperLength = 0.2f,
				noiseSeed = 1
			};
		}
	}

	readonly List<Vector3> vertices = new List<Vector3>(256);
	readonly List<Vector3> normals = new List<Vector3>(256);
	readonly List<Vector2> uvs = new List<Vector2>(256);
	readonly List<int> triangles = new List<int>(512);

	public void Generate(Mesh mesh, Vector2[] spine, float planeZ, Transform space, in LeechMeshConfigData config)
	{
		vertices.Clear();
		normals.Clear();
		uvs.Clear();
		triangles.Clear();

		int ringCount = spine.Length;
		int radial = Mathf.Max(3, config.radialSegments);
		if (ringCount < 2)
		{
			mesh.Clear();
			return;
		}

		Vector3[] centers = new Vector3[ringCount];
		Vector3[] tangents = new Vector3[ringCount];
		Vector3[] sides = new Vector3[ringCount];
		Vector3[] ups = new Vector3[ringCount];
		float[] halfWidths = new float[ringCount];
		float[] halfHeights = new float[ringCount];

		for (int i = 0; i < ringCount; i++)
			centers[i] = space.InverseTransformPoint(new Vector3(spine[i].x, spine[i].y, planeZ));

		for (int i = 0; i < ringCount; i++)
		{
			Vector3 tangent;
			if (i == 0)
				tangent = centers[1] - centers[0];
			else if (i == ringCount - 1)
				tangent = centers[i] - centers[i - 1];
			else
				tangent = centers[i + 1] - centers[i - 1];

			if (tangent.sqrMagnitude < 1e-8f)
				tangent = Vector3.left;
			tangents[i] = tangent.normalized;

			float t = i / (float)(ringCount - 1);
			SampleCrossSection(config, t, out halfWidths[i], out halfHeights[i]);
		}

		BuildFrames(tangents, sides, ups);

		for (int i = 0; i < ringCount; i++)
		{
			float v = i / (float)(ringCount - 1);
			for (int r = 0; r < radial; r++)
			{
				float u = r / (float)radial;
				float angle = u * Mathf.PI * 2f;
				float cos = Mathf.Cos(angle);
				float sin = Mathf.Sin(angle);

				Vector3 offset = sides[i] * (cos * halfWidths[i]) + ups[i] * (sin * halfHeights[i]);
				Vector3 normal = (sides[i] * (cos * halfHeights[i]) + ups[i] * (sin * halfWidths[i])).normalized;
				if (normal.sqrMagnitude < 1e-8f)
					normal = ups[i];

				vertices.Add(centers[i] + offset);
				normals.Add(normal);
				uvs.Add(new Vector2(u, v));
			}
		}

		for (int i = 0; i < ringCount - 1; i++)
		{
			int ringA = i * radial;
			int ringB = (i + 1) * radial;
			for (int r = 0; r < radial; r++)
			{
				int next = (r + 1) % radial;
				int a = ringA + r;
				int b = ringA + next;
				int c = ringB + r;
				int d = ringB + next;

				triangles.Add(a);
				triangles.Add(b);
				triangles.Add(c);
				triangles.Add(b);
				triangles.Add(d);
				triangles.Add(c);
			}
		}

		// Tail cap (closed end). Head stays open as the mouth.
		int tailRing = (ringCount - 1) * radial;
		int tailCenter = vertices.Count;
		vertices.Add(centers[ringCount - 1]);
		normals.Add(-tangents[ringCount - 1]);
		uvs.Add(new Vector2(0.5f, 1f));

		for (int r = 0; r < radial; r++)
		{
			int next = (r + 1) % radial;
			triangles.Add(tailCenter);
			triangles.Add(tailRing + r);
			triangles.Add(tailRing + next);
		}

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetNormals(normals);
		mesh.SetUVs(0, uvs);
		mesh.SetTriangles(triangles, 0, true);
		mesh.RecalculateBounds();
	}

	static void SampleCrossSection(in LeechMeshConfigData config, float t, out float halfWidth, out float halfHeight)
	{
		float widthNoise = SampleNoiseOrDefault(config.width, t, config.noiseSeed);
		float heightNoise = SampleNoiseOrDefault(config.height, t, config.noiseSeed + 7919u);

		float bodyWidth = config.baseWidth * Mathf.Lerp(0.55f, 1.2f, widthNoise);
		float bodyHeight = config.baseHeight * Mathf.Lerp(0.55f, 1.2f, heightNoise);

		float mouthBlend = 1f - Mathf.SmoothStep(0f, Mathf.Max(0.01f, config.mouthBlendLength), t);
		halfWidth = Mathf.Lerp(bodyWidth, config.mouthSize, mouthBlend);
		halfHeight = Mathf.Lerp(bodyHeight, config.mouthSize * 0.65f, mouthBlend);

		float tail = Mathf.SmoothStep(0f, 1f, (1f - t) / Mathf.Max(0.01f, config.tailTaperLength));
		halfWidth *= Mathf.Lerp(0.05f, 1f, tail);
		halfHeight *= Mathf.Lerp(0.05f, 1f, tail);
	}

	static float SampleNoiseOrDefault(List<NoiseLayer> layers, float t, uint seed)
	{
		if (layers == null || layers.Count == 0)
			return 0.5f;

		return NoiseField.SampleComposed(layers, t, 0.5f, seed);
	}

	static void BuildFrames(Vector3[] tangents, Vector3[] sides, Vector3[] ups)
	{
		Vector3 side = Vector3.Cross(tangents[0], Vector3.forward);
		if (side.sqrMagnitude < 1e-8f)
			side = Vector3.Cross(tangents[0], Vector3.up);
		sides[0] = side.normalized;
		ups[0] = Vector3.Cross(sides[0], tangents[0]).normalized;

		for (int i = 1; i < tangents.Length; i++)
		{
			Vector3 projected = sides[i - 1] - tangents[i] * Vector3.Dot(sides[i - 1], tangents[i]);
			if (projected.sqrMagnitude < 1e-8f)
			{
				projected = ups[i - 1] - tangents[i] * Vector3.Dot(ups[i - 1], tangents[i]);
			}

			sides[i] = projected.normalized;
			ups[i] = Vector3.Cross(sides[i], tangents[i]).normalized;
		}
	}
}

public class Chain
{
	private Vector2[] chainPositions;
	private float[] chainLengths;
	private float maxBendDegrees;

	public Vector2[] Positions => chainPositions;

	public float MaxBendDegrees
	{
		get => maxBendDegrees;
		set => maxBendDegrees = Mathf.Clamp(value, 1f, 180f);
	}

	public Chain(List<float> segmentLengths, Vector2 origin, float maxBendDegrees = 35f)
	{
		int segmentCount = segmentLengths.Count;
		chainLengths = new float[segmentCount];
		chainPositions = new Vector2[segmentCount + 1];
		MaxBendDegrees = maxBendDegrees;

		for (int i = 0; i < segmentCount; i++)
			chainLengths[i] = segmentLengths[i];

		chainPositions[0] = origin;
		for (int i = 0; i < segmentCount; i++)
			chainPositions[i + 1] = chainPositions[i] + Vector2.left * chainLengths[i];
	}

	public void SetChainRootPosition(Vector2 position)
	{
		chainPositions[0] = position;
		for (int i = 1; i < chainPositions.Length; i++)
		{
			chainPositions[i] = ConstrainToLength(chainPositions[i], chainPositions[i - 1], chainLengths[i - 1]);

			if (i >= 2)
				chainPositions[i] = ConstrainBend(chainPositions[i - 2], chainPositions[i - 1], chainPositions[i], chainLengths[i - 1], maxBendDegrees);
		}
	}

	/// <summary>
	/// Places <paramref name="point"/> on the circle of radius <paramref name="length"/>
	/// around <paramref name="anchor"/>, preserving direction from anchor toward the old point.
	/// </summary>
	public static Vector2 ConstrainToLength(Vector2 point, Vector2 anchor, float length)
	{
		Vector2 offset = point - anchor;
		float distance = offset.magnitude;
		if (distance < 1e-6f)
			return anchor + Vector2.left * length;

		return anchor + offset * (length / distance);
	}

	/// <summary>
	/// Limits how sharply the segment from <paramref name="prev"/>→<paramref name="joint"/>
	/// to <paramref name="joint"/>→<paramref name="next"/> may bend, keeping segment length.
	/// </summary>
	public static Vector2 ConstrainBend(Vector2 prev, Vector2 joint, Vector2 next, float length, float maxBendDegrees)
	{
		Vector2 incoming = joint - prev;
		if (incoming.sqrMagnitude < 1e-8f)
			return next;

		Vector2 outgoing = next - joint;
		if (outgoing.sqrMagnitude < 1e-8f)
			return joint + incoming.normalized * length;

		float signedDegrees = Vector2.SignedAngle(incoming, outgoing);
		float clampedDegrees = Mathf.Clamp(signedDegrees, -maxBendDegrees, maxBendDegrees);
		if (Mathf.Approximately(clampedDegrees, signedDegrees))
			return next;

		float rad = clampedDegrees * Mathf.Deg2Rad;
		float cos = Mathf.Cos(rad);
		float sin = Mathf.Sin(rad);
		Vector2 dir = incoming.normalized;
		Vector2 limited = new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
		return joint + limited * length;
	}
}
