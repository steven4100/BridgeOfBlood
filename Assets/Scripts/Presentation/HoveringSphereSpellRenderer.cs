using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Diegetic <see cref="SpellRenderer"/>: a sphere that hovers above the playfield at half the spell's
/// resolved AoE radius. On cast it crashes to the ground over <see cref="SpellCastForecast.spawnTime"/>
/// (matching attack-entity emit), then slowly rises back to hover height.
///
/// Height is on local Z — the simulation zone lies flat, so Z is "up" from the ground plane, and
/// <see cref="SpellRenderer"/> already preserves Z while syncing XY to the cast origin.
/// Hover/impact heights are derived from the visual radius so the sphere clears (then meets) the ground
/// regardless of AoE size.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class HoveringSphereSpellRenderer : SpellRenderer
{
	[Tooltip("Gap between the sphere's bottom and the ground while hovering.")]
	[SerializeField, Min(0f)] float hoverClearance = 4f;
	[Tooltip("How far past ground-contact the sphere sinks at impact (0 = sits on the ground).")]
	[SerializeField, Min(0f)] float impactSink = 0f;
	[Tooltip("Seconds to rise from impact back to hover after the crash. Independent of spawn timing.")]
	[SerializeField, Min(0.05f)] float riseDuration = 1.25f;
	[Tooltip("Visual radius = resolved AoE radius × this. 0.5 = half the spell's hitbox.")]
	[SerializeField, Min(0.01f)] float aoeRadiusScale = 0.5f;
	[SerializeField, Min(0.1f)] float minRadius = 0.5f;
	[SerializeField] Color idleColor = new Color(0.55f, 0.2f, 0.75f, 0.85f);
	[SerializeField] Color onDeckColor = new Color(0.9f, 0.2f, 0.35f, 0.95f);
	[SerializeField] Color impactColor = new Color(1f, 0.7f, 0.2f, 1f);

	enum Phase
	{
		Hovering,
		Crashing,
		Rising
	}

	Mesh _mesh;
	Material _material;
	bool _ownsMesh;
	float _visualRadius = 1f;
	Phase _phase = Phase.Hovering;
	float _phaseElapsed;
	float _crashDuration;
	float _heightFrom;
	float _heightTo;

	float HoverHeight => _visualRadius + hoverClearance;
	float ImpactHeight => Mathf.Max(0f, _visualRadius - impactSink);

	void Awake()
	{
		EnsureResources();
		SetHeight(HoverHeight);
		ApplyColor();
	}

	void Update()
	{
		if (_phase == Phase.Hovering)
			return;

		_phaseElapsed += Time.deltaTime;
		float duration = _phase == Phase.Crashing ? _crashDuration : riseDuration;

		if (duration <= 0f || _phaseElapsed >= duration)
		{
			SetHeight(_heightTo);
			if (_phase == Phase.Crashing)
				BeginRise();
			else
				BeginHover();
			return;
		}

		float t = _phaseElapsed / duration;
		// Crash eases in (heavy drop); rise eases out (settle into hover).
		float eased = _phase == Phase.Crashing ? t * t : 1f - (1f - t) * (1f - t);
		SetHeight(Mathf.Lerp(_heightFrom, _heightTo, eased));
		ApplyColor();
	}

	protected override void OnForecastChanged()
	{
		ApplyScale(Spell.CurrentForecast.hitBox);
		if (_phase == Phase.Hovering)
			SetHeight(HoverHeight);
	}

	protected override void OnCastInvoked()
	{
		BeginCrash(Spell.LastCastForecast.spawnTime);
	}

	protected override void OnDeckChanged(bool isOnDeck)
	{
		ApplyColor();
	}

	protected override void OnBound()
	{
		EnsureResources();
		if (_phase == Phase.Hovering)
			SetHeight(HoverHeight);
		ApplyColor();
	}

	protected override void OnUnbound()
	{
		BeginHover();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (_ownsMesh && _mesh != null) Destroy(_mesh);
		if (_material != null) Destroy(_material);
	}

	void BeginCrash(float spawnTime)
	{
		_crashDuration = Mathf.Max(0f, spawnTime);
		_heightFrom = CurrentHeight();
		_heightTo = ImpactHeight;
		_phaseElapsed = 0f;

		if (_crashDuration <= 0f)
		{
			SetHeight(ImpactHeight);
			BeginRise();
			return;
		}

		_phase = Phase.Crashing;
		ApplyColor();
	}

	void BeginRise()
	{
		_heightFrom = CurrentHeight();
		_heightTo = HoverHeight;
		_phaseElapsed = 0f;
		_phase = Phase.Rising;
		ApplyColor();
	}

	void BeginHover()
	{
		_phase = Phase.Hovering;
		_phaseElapsed = 0f;
		SetHeight(HoverHeight);
		ApplyColor();
	}

	void ApplyScale(in HitBoxData hitBox)
	{
		float aoeRadius = ResolveAoeRadius(hitBox);
		_visualRadius = Mathf.Max(minRadius, aoeRadius * aoeRadiusScale);
		// Builtin / unit sphere mesh has diameter 1 (radius 0.5), so scale = diameter.
		float diameter = _visualRadius * 2f;
		transform.localScale = new Vector3(diameter, diameter, diameter);
	}

	void ApplyColor()
	{
		EnsureResources();
		bool isOnDeck = Spell != null && Spell.IsOnDeck;
		if (_phase == Phase.Crashing)
			_material.color = impactColor;
		else
			_material.color = isOnDeck ? onDeckColor : idleColor;
	}

	void SetHeight(float height)
	{
		Vector3 local = transform.localPosition;
		transform.localPosition = new Vector3(local.x, local.y, height);
	}

	float CurrentHeight() => transform.localPosition.z;

	void EnsureResources()
	{
		if (_mesh == null)
		{
			Mesh builtin = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
			if (builtin != null)
			{
				_mesh = builtin;
				_ownsMesh = false;
			}
			else
			{
				_mesh = CreateUnitSphereMesh();
				_ownsMesh = true;
			}
			GetComponent<MeshFilter>().sharedMesh = _mesh;
		}

		if (_material == null)
		{
			var shader = Shader.Find("Sprites/Default");
			if (shader == null)
				shader = Shader.Find("Universal Render Pipeline/Lit");
			_material = new Material(shader);
			GetComponent<MeshRenderer>().material = _material;
		}
	}

	static float ResolveAoeRadius(in HitBoxData hitBox)
	{
		if (hitBox.isSphere)
			return hitBox.sphereRadius;
		if (hitBox.isRect)
			return Mathf.Max(hitBox.rectDimension.x, hitBox.rectDimension.y) * 0.5f;
		return 1f;
	}

	/// <summary>Low-poly unit sphere (diameter 1) used when the builtin mesh is unavailable.</summary>
	static Mesh CreateUnitSphereMesh()
	{
		const int lon = 16;
		const int lat = 12;
		var mesh = new Mesh { name = "HoveringSpellSphere" };

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
