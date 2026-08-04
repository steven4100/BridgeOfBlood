using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Base for diegetic per-spell visuals: one instance per loop slot, instantiated from
/// <see cref="SpellAuthoringData.rendererPrefab"/> by <see cref="SpellRenderManager"/>.
///
/// Bound to a <see cref="RuntimeSpell"/> and driven by that spell's own change events, so a renderer only does
/// work when its spell's data actually changes. Read <see cref="RuntimeSpell.CurrentForecast"/> to shape the
/// on-deck preview (projectile counts, area sizes) and <see cref="RuntimeSpell.LastCastForecast"/> plus
/// <see cref="RuntimeSpell.RoundTimeInvokedAt"/> to sync a cast animation to the actual spawn timing.
///
/// Every cast originates at the player, so this base class places the instance at the player position plus the
/// forecast's <see cref="SpellCastForecast.originOffset"/>; subclasses draw in local space around that origin.
/// Instances must live under the simulation zone rect, where local space is simulation space.
/// </summary>
public abstract class SpellRenderer : MonoBehaviour
{
	/// <summary>The bound loop slot, or null while unbound.</summary>
	public RuntimeSpell Spell { get; private set; }

	Vector2 _playerPosition;

	/// <summary>Binds to a loop slot and applies its current state. Rebinding unbinds the previous spell first.</summary>
	public void Bind(RuntimeSpell spell)
	{
		if (ReferenceEquals(Spell, spell))
			return;

		Unbind();

		Spell = spell;
		spell.CurrentForecastChanged += HandleForecastChanged;
		spell.CastInvoked += OnCastInvoked;
		spell.OnDeckChanged += HandleOnDeckChanged;

		OnBound();
		HandleForecastChanged();
		HandleOnDeckChanged();
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

		Spell.CurrentForecastChanged -= HandleForecastChanged;
		Spell.CastInvoked -= OnCastInvoked;
		Spell.OnDeckChanged -= HandleOnDeckChanged;
		Spell = null;

		OnUnbound();
	}

	protected virtual void OnDestroy()
	{
		Unbind();
	}

	/// <summary>Called after <see cref="Spell"/> is set, before the initial forecast/on-deck callbacks.</summary>
	protected virtual void OnBound() { }

	/// <summary>Called after <see cref="Spell"/> is cleared.</summary>
	protected virtual void OnUnbound() { }

	/// <summary>
	/// The bound spell's forecast changed (or was just bound). Re-read <see cref="RuntimeSpell.CurrentForecast"/>
	/// and reshape the preview.
	/// </summary>
	protected abstract void OnForecastChanged();

	/// <summary>
	/// The bound spell was cast. Read <see cref="RuntimeSpell.LastCastForecast"/> and
	/// <see cref="RuntimeSpell.RoundTimeInvokedAt"/> to drive a cast animation.
	/// </summary>
	protected abstract void OnCastInvoked();

	/// <summary>The bound spell became (or stopped being) the next spell to cast.</summary>
	protected abstract void OnDeckChanged(bool isOnDeck);

	void HandleForecastChanged()
	{
		ApplyOrigin();
		OnForecastChanged();
	}

	void HandleOnDeckChanged()
	{
		OnDeckChanged(Spell.IsOnDeck);
	}

	/// <summary>Places the instance on its spell's spawn point: player position + the forecast's emitter offset.</summary>
	void ApplyOrigin()
	{
		Vector2 origin = _playerPosition + Spell.CurrentForecast.originOffset;
		Vector3 local = transform.localPosition;
		transform.localPosition = new Vector3(origin.x, origin.y, local.z);
	}
}
