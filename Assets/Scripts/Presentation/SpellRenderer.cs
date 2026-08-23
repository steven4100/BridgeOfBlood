using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Base for diegetic per-spell visuals: one instance per loop slot, instantiated from
/// <see cref="SpellAuthoringData.rendererPrefab"/> by <see cref="SpellRenderManager"/>.
///
/// Bound to a <see cref="RuntimeSpell"/> and driven by that spell's cast/on-deck events plus per-frame
/// <see cref="SyncFrame"/> calls from the manager. Subclasses resolve preview fields from authoring data and
/// frame modifications, rebuilding visuals only when resolved values change.
///
/// Every cast originates at the player, so this base class places the instance at the player position plus the
/// emitter's authoring offset; subclasses draw in local space around that origin.
/// Instances must live under the simulation zone rect, where local space is simulation space.
/// </summary>
public abstract class SpellRenderer : MonoBehaviour
{
	/// <summary>The bound loop slot, or null while unbound.</summary>
	public RuntimeSpell Spell { get; private set; }

	Vector2 _playerPosition;
	bool _previewDirty = true;

	/// <summary>Resolved modifications for the current simulation frame.</summary>
	protected SpellModifications FrameMods { get; private set; }

	protected SpellKeyFrame PrimaryKeyFrame => Spell.Definition.SpellAnimation.keyFrames[0];

	protected SpellAttributeMask AttributeMask => Spell.Definition.attributeMask;

	/// <summary>Binds to a loop slot and applies its current state. Rebinding unbinds the previous spell first.</summary>
	public void Bind(RuntimeSpell spell)
	{
		if (ReferenceEquals(Spell, spell))
			return;

		Unbind();

		Spell = spell;
		spell.CastInvoked += OnCastInvoked;
		spell.OnDeckChanged += HandleOnDeckChanged;

		InvalidatePreviewCache();
		OnBound();
		HandleOnDeckChanged();
	}

	/// <summary>
	/// Pushes frame modifications and refreshes the preview when resolved values change.
	/// Called each simulation frame by <see cref="SpellRenderManager"/> for bound renderers only.
	/// </summary>
	public void SyncFrame(SpellModifications mods)
	{
		FrameMods = mods;
		ApplyOrigin();

		if (_previewDirty || IsPreviewDirty())
		{
			_previewDirty = false;
			OnPreviewRefresh();
		}
	}

	/// <summary>
	/// Pushes the current player position (simulation space) so the instance can sit on the cast origin.
	/// Called each simulation frame by <see cref="SpellRenderManager"/> for bound renderers only.
	/// </summary>
	public void SyncOrigin(Vector2 playerPosition)
	{
		if (_playerPosition == playerPosition)
			return;

		_playerPosition = playerPosition;
		ApplyOrigin();
	}

	public void Unbind()
	{
		if (Spell == null)
			return;

		Spell.CastInvoked -= OnCastInvoked;
		Spell.OnDeckChanged -= HandleOnDeckChanged;
		Spell = null;
		FrameMods = null;

		OnUnbound();
	}

	protected virtual void OnDestroy()
	{
		Unbind();
	}

	/// <summary>Called after <see cref="Spell"/> is set, before the initial on-deck callback.</summary>
	protected virtual void OnBound() { }

	/// <summary>Called after <see cref="Spell"/> is cleared.</summary>
	protected virtual void OnUnbound() { }

	/// <summary>
	/// Resolves preview fields from <see cref="FrameMods"/> and returns whether they differ from the cached snapshot.
	/// Updates the cache when values change.
	/// </summary>
	protected abstract bool IsPreviewDirty();

	/// <summary>Rebuild preview visuals from the cached resolved values (updated by <see cref="IsPreviewDirty"/>).</summary>
	protected abstract void OnPreviewRefresh();

	/// <summary>
	/// The bound spell was cast. Read <see cref="PrimaryKeyFrame"/>.time to drive cast animation timing.
	/// </summary>
	protected abstract void OnCastInvoked();

	/// <summary>The bound spell became (or stopped being) the next spell to cast.</summary>
	protected abstract void OnDeckChanged(bool isOnDeck);

	protected virtual void InvalidatePreviewCache()
	{
		_previewDirty = true;
	}

	protected static bool HitBoxEquals(in HitBoxData a, in HitBoxData b)
	{
		return a.isSphere == b.isSphere
			&& a.isRect == b.isRect
			&& a.sphereRadius == b.sphereRadius
			&& a.rectDimension == b.rectDimension
			&& a.scaleGrowthRate == b.scaleGrowthRate;
	}

	void HandleOnDeckChanged()
	{
		OnDeckChanged(Spell.IsOnDeck);
	}

	/// <summary>Places the instance on its spell's spawn point: player position + the emitter's authoring offset.</summary>
	void ApplyOrigin()
	{
		Vector2 offset = PrimaryKeyFrame.attackEntityEmitter.relativeToPlayerSpawnCriteria.offsetFromPlayer;
		Vector2 origin = _playerPosition + offset;
		Vector3 local = transform.localPosition;
		transform.localPosition = new Vector3(origin.x, origin.y, local.z);
	}
}
