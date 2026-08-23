using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Reference <see cref="SpellRenderer"/> implementation, drawn into the game view via a procedural line mesh:
/// one ray per emitted entity across the resolved spread, plus an outline of the resolved hitbox. Tints red
/// while on deck and flashes on cast.
///
/// The mesh is rebuilt only when resolved preview values change. Real spells replace this with diegetic art
/// driven by the same resolution path.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class DebugSpellRenderer : SpellRenderer
{
	const int RingSegments = 32;

	[SerializeField, Min(0.1f)] float rayLength = 3f;
	[SerializeField, Min(0.01f)] float lineThickness = 0.15f;
	[Tooltip("Direction the emit rays fan around. 180 matches the simulation's leftward cast forward.")]
	[SerializeField, Range(-180f, 180f)] float forwardDegrees = 180f;
	[SerializeField, Min(0.05f)] float castFlashDuration = 0.25f;
	[SerializeField] Color idleColor = new Color(0.5f, 0.5f, 0.55f, 0.5f);
	[SerializeField] Color onDeckColor = new Color(0.85f, 0.15f, 0.2f, 0.9f);
	[SerializeField] Color castColor = new Color(1f, 0.85f, 0.3f, 1f);

	readonly List<Vector3> _vertices = new List<Vector3>();
	readonly List<int> _triangles = new List<int>();

	Mesh _mesh;
	Material _material;
	float _castFlashRemaining;

	int _lastEmitCount = -1;
	float _lastSpreadDegrees;
	HitBoxData _lastHitBox;

	void Awake()
	{
		EnsureResources();
		ApplyColor();
	}

	/// <summary>
	/// Creates the mesh and material instance. Called from both <c>Awake</c> and the draw paths so binding
	/// works regardless of whether the instance was active when it was created.
	/// </summary>
	void EnsureResources()
	{
		if (_mesh == null)
		{
			_mesh = new Mesh { name = "DebugSpellPreview" };
			_mesh.MarkDynamic();
			GetComponent<MeshFilter>().mesh = _mesh;
		}

		if (_material == null)
		{
			_material = new Material(Shader.Find("Sprites/Default"));
			GetComponent<MeshRenderer>().material = _material;
		}
	}

	void Update()
	{
		if (_castFlashRemaining <= 0f)
			return;

		_castFlashRemaining -= Time.deltaTime;
		ApplyColor();
	}

	protected override void InvalidatePreviewCache()
	{
		base.InvalidatePreviewCache();
		_lastEmitCount = -1;
	}

	protected override bool IsPreviewDirty()
	{
		SpellKeyFrame keyFrame = PrimaryKeyFrame;
		AttackEntityEmitter emitter = keyFrame.attackEntityEmitter;
		AttackEntityData attackData = keyFrame.attackEntityData;
		SpellAttributeMask mask = AttributeMask;

		int emitCount = SpellModificationsApplicator.ResolveEmitCount(FrameMods, emitter.baseEmitCount, mask);
		float spreadDegrees = emitter.spreadDegrees;
		HitBoxData hitBox = AttackEntityModificationApplicator.ResolveHitBox(attackData.hitBoxData, FrameMods, mask);

		if (emitCount == _lastEmitCount
			&& spreadDegrees == _lastSpreadDegrees
			&& HitBoxEquals(hitBox, _lastHitBox))
			return false;

		_lastEmitCount = emitCount;
		_lastSpreadDegrees = spreadDegrees;
		_lastHitBox = hitBox;
		return true;
	}

	protected override void OnPreviewRefresh()
	{
		RebuildMesh(_lastEmitCount, _lastSpreadDegrees, _lastHitBox);
	}

	protected override void OnCastInvoked()
	{
		_castFlashRemaining = castFlashDuration;
		ApplyColor();
	}

	protected override void OnDeckChanged(bool isOnDeck)
	{
		ApplyColor();
	}

	protected override void OnUnbound()
	{
		_vertices.Clear();
		_triangles.Clear();
		UploadMesh();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (_mesh != null) Destroy(_mesh);
		if (_material != null) Destroy(_material);
	}

	void ApplyColor()
	{
		EnsureResources();
		bool isOnDeck = Spell != null && Spell.IsOnDeck;
		_material.color = _castFlashRemaining > 0f
			? castColor
			: (isOnDeck ? onDeckColor : idleColor);
	}

	void RebuildMesh(int emitCount, float spreadDegrees, in HitBoxData hitBox)
	{
		_vertices.Clear();
		_triangles.Clear();

		AddEmitRays(emitCount, spreadDegrees);
		AddHitBoxOutline(hitBox);

		UploadMesh();
	}

	void AddEmitRays(int emitCount, float spreadDegrees)
	{
		int count = Mathf.Max(1, emitCount);
		float startDegrees = forwardDegrees - spreadDegrees * 0.5f;
		float stepDegrees = count > 1 ? spreadDegrees / count : 0f;

		for (int i = 0; i < count; i++)
		{
			float radians = Mathf.Deg2Rad * (startDegrees + stepDegrees * i);
			var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
			AddLine(Vector2.zero, direction * rayLength);
		}
	}

	void AddHitBoxOutline(in HitBoxData hitBox)
	{
		if (hitBox.isSphere)
			AddRing(hitBox.sphereRadius);
		else if (hitBox.isRect)
			AddRectOutline(hitBox.rectDimension);
	}

	void AddRing(float radius)
	{
		var previous = new Vector2(radius, 0f);
		for (int i = 1; i <= RingSegments; i++)
		{
			float radians = Mathf.PI * 2f * i / RingSegments;
			var current = new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
			AddLine(previous, current);
			previous = current;
		}
	}

	void AddRectOutline(Vector2 size)
	{
		Vector2 half = size * 0.5f;
		var bottomLeft = new Vector2(-half.x, -half.y);
		var bottomRight = new Vector2(half.x, -half.y);
		var topRight = new Vector2(half.x, half.y);
		var topLeft = new Vector2(-half.x, half.y);

		AddLine(bottomLeft, bottomRight);
		AddLine(bottomRight, topRight);
		AddLine(topRight, topLeft);
		AddLine(topLeft, bottomLeft);
	}

	/// <summary>Appends a thickened quad so lines are visible to a normal camera (unlike Gizmos).</summary>
	void AddLine(Vector2 from, Vector2 to)
	{
		Vector2 delta = to - from;
		if (delta.sqrMagnitude < 0.000001f)
			return;

		Vector2 offset = new Vector2(-delta.y, delta.x).normalized * (lineThickness * 0.5f);
		int baseIndex = _vertices.Count;

		_vertices.Add(from - offset);
		_vertices.Add(from + offset);
		_vertices.Add(to + offset);
		_vertices.Add(to - offset);

		_triangles.Add(baseIndex);
		_triangles.Add(baseIndex + 1);
		_triangles.Add(baseIndex + 2);
		_triangles.Add(baseIndex);
		_triangles.Add(baseIndex + 2);
		_triangles.Add(baseIndex + 3);
	}

	void UploadMesh()
	{
		EnsureResources();
		_mesh.Clear();
		_mesh.SetVertices(_vertices);
		_mesh.SetTriangles(_triangles, 0);
		_mesh.RecalculateBounds();
	}
}
