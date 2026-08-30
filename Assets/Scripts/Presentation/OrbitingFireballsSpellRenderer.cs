using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Diegetic <see cref="SpellRenderer"/> for Fireball: one sphere per resolved projectile, equally spaced
/// on a ring around the cast origin (the player) and spinning in the playfield XY plane.
///
/// Sphere size tracks the resolved hitbox; count tracks <see cref="SpellModificationsApplicator.ResolveEmitCount"/>.
/// Children are pooled and only rebuilt when those resolved values change.
/// </summary>
public sealed class OrbitingFireballsSpellRenderer : SpellRenderer
{
	[Tooltip("Distance from the player to each fireball's center.")]
	[SerializeField, Min(0.1f)] float orbitRadius = 40f;
	[Tooltip("Degrees per second while idle. On-deck and cast multiply this.")]
	[SerializeField] float orbitDegreesPerSecond = 90f;
	[SerializeField, Min(1f)] float onDeckSpeedMultiplier = 1.6f;
	[SerializeField, Min(1f)] float castSpeedMultiplier = 2.4f;
	[SerializeField, Min(0.05f)] float castFlashDuration = 0.35f;
	[Tooltip("Local Z (up from the ground plane) so the orbs clear the playfield.")]
	[SerializeField] float hoverHeight = 8f;
	[Tooltip("Visual radius = resolved hitbox radius × this.")]
	[SerializeField, Min(0.01f)] float hitBoxRadiusScale = 0.25f;
	[SerializeField, Min(0.1f)] float minRadius = 4f;
	[SerializeField] Color idleColor = new Color(1f, 0.35f, 0.08f, 0.9f);
	[SerializeField] Color onDeckColor = new Color(1f, 0.55f, 0.15f, 1f);
	[SerializeField] Color castColor = new Color(1f, 0.9f, 0.4f, 1f);

	readonly List<Transform> _orbs = new List<Transform>();

	Mesh _sphereMesh;
	Material _material;
	bool _ownsMesh;
	int _lastEmitCount = -1;
	float _lastVisualRadius = -1f;
	HitBoxData _lastHitBox;
	float _orbitAngle;
	float _castFlashRemaining;
	float _visualRadius = 4f;

	void Awake()
	{
		EnsureResources();
		ApplyColor();
	}

	void Update()
	{
		if (_castFlashRemaining > 0f)
		{
			_castFlashRemaining -= Time.deltaTime;
			if (_castFlashRemaining <= 0f)
				ApplyColor();
		}

		if (_orbs.Count == 0 || Spell == null || !Spell.IsOnDeck)
			return;

		float speed = orbitDegreesPerSecond * onDeckSpeedMultiplier;
		if (_castFlashRemaining > 0f)
			speed *= castSpeedMultiplier;

		_orbitAngle += speed * Time.deltaTime;
		if (_orbitAngle > 360f)
			_orbitAngle -= 360f;

		PlaceOrbs();
	}

	protected override void InvalidatePreviewCache()
	{
		base.InvalidatePreviewCache();
		_lastEmitCount = -1;
		_lastVisualRadius = -1f;
	}

	protected override bool IsPreviewDirty()
	{
		SpellKeyFrame keyFrame = PrimaryKeyFrame;
		int emitCount = SpellModificationsApplicator.ResolveEmitCount(
			FrameMods, keyFrame.attackEntityEmitter.baseEmitCount, AttributeMask);
		HitBoxData hitBox = AttackEntityModificationApplicator.ResolveHitBox(
			keyFrame.attackEntityData, FrameMods, AttributeMask);
		float visualRadius = Mathf.Max(minRadius, ResolveHitBoxRadius(hitBox) * hitBoxRadiusScale);

		if (emitCount == _lastEmitCount
			&& visualRadius == _lastVisualRadius
			&& HitBoxEquals(hitBox, _lastHitBox))
			return false;

		_lastEmitCount = emitCount;
		_lastVisualRadius = visualRadius;
		_lastHitBox = hitBox;
		return true;
	}

	protected override void OnPreviewRefresh()
	{
		_visualRadius = _lastVisualRadius;
		EnsureOrbCount(_lastEmitCount);
		ApplyOrbScale();
		PlaceOrbs();
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

	protected override void OnBound()
	{
		EnsureResources();
		ApplyColor();
	}

	protected override void OnUnbound()
	{
		_castFlashRemaining = 0f;
		EnsureOrbCount(0);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (_ownsMesh && _sphereMesh != null)
			Destroy(_sphereMesh);
		if (_material != null)
			Destroy(_material);
	}

	void EnsureOrbCount(int count)
	{
		EnsureResources();
		count = Mathf.Max(0, count);

		while (_orbs.Count < count)
		{
			var go = new GameObject("FireballOrb");
			go.transform.SetParent(transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
			go.AddComponent<MeshRenderer>().sharedMaterial = _material;
			_orbs.Add(go.transform);
		}

		for (int i = 0; i < _orbs.Count; i++)
			_orbs[i].gameObject.SetActive(i < count);
	}

	void ApplyOrbScale()
	{
		float diameter = _visualRadius * 2f;
		var scale = new Vector3(diameter, diameter, diameter);
		int active = Mathf.Max(0, _lastEmitCount);
		for (int i = 0; i < active && i < _orbs.Count; i++)
			_orbs[i].localScale = scale;
	}

	void PlaceOrbs()
	{
		int count = Mathf.Max(0, _lastEmitCount);
		if (count == 0)
			return;

		float step = 360f / count;
		for (int i = 0; i < count && i < _orbs.Count; i++)
		{
			float radians = Mathf.Deg2Rad * (_orbitAngle + step * i);
			_orbs[i].localPosition = new Vector3(
				Mathf.Cos(radians) * orbitRadius,
				Mathf.Sin(radians) * orbitRadius,
				hoverHeight);
		}
	}

	void ApplyColor()
	{
		EnsureResources();
		bool isOnDeck = Spell != null && Spell.IsOnDeck;
		_material.color = _castFlashRemaining > 0f
			? castColor
			: (isOnDeck ? onDeckColor : idleColor);
	}

	void EnsureResources()
	{
		if (_sphereMesh == null)
		{
			Mesh builtin = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
			if (builtin != null)
			{
				_sphereMesh = builtin;
				_ownsMesh = false;
			}
			else
			{
				_sphereMesh = CreateUnitSphereMesh();
				_ownsMesh = true;
			}
		}

		if (_material == null)
		{
			var shader = Shader.Find("Sprites/Default");
			if (shader == null)
				shader = Shader.Find("Universal Render Pipeline/Lit");
			_material = new Material(shader);
		}
	}

	static float ResolveHitBoxRadius(in HitBoxData hitBox)
	{
		if (hitBox.isSphere)
			return hitBox.sphereRadius;
		if (hitBox.isRect)
			return Mathf.Max(hitBox.rectDimension.x, hitBox.rectDimension.y) * 0.5f;
		return 1f;
	}

	static Mesh CreateUnitSphereMesh()
	{
		const int lon = 16;
		const int lat = 12;
		var mesh = new Mesh { name = "OrbitingFireballSphere" };

		int vertCount = (lat + 1) * (lon + 1);
		var verts = new Vector3[vertCount];
		var norms = new Vector3[vertCount];
		int vi = 0;
		for (int y = 0; y <= lat; y++)
		{
			float v = y / (float)lat;
			float pitch = (v - 0.5f) * Mathf.PI;
			float yPos = Mathf.Sin(pitch) * 0.5f;
			float radius = Mathf.Cos(pitch) * 0.5f;
			for (int x = 0; x <= lon; x++)
			{
				float u = x / (float)lon;
				float yaw = u * Mathf.PI * 2f;
				var p = new Vector3(Mathf.Cos(yaw) * radius, yPos, Mathf.Sin(yaw) * radius);
				verts[vi] = p;
				norms[vi] = p.normalized;
				vi++;
			}
		}

		var tris = new int[lat * lon * 6];
		int ti = 0;
		for (int y = 0; y < lat; y++)
		{
			for (int x = 0; x < lon; x++)
			{
				int i0 = y * (lon + 1) + x;
				int i1 = i0 + 1;
				int i2 = i0 + (lon + 1);
				int i3 = i2 + 1;
				tris[ti++] = i0;
				tris[ti++] = i2;
				tris[ti++] = i1;
				tris[ti++] = i1;
				tris[ti++] = i2;
				tris[ti++] = i3;
			}
		}

		mesh.vertices = verts;
		mesh.normals = norms;
		mesh.triangles = tris;
		mesh.RecalculateBounds();
		return mesh;
	}
}
