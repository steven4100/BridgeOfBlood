using BridgeOfBlood.Data.Shared;
using UnityEngine;

/// <summary>
/// Syncs the in-game SimulationRect <see cref="RectTransform"/> from simulation playfield data
/// so <c>zone.rect</c> matches the playfield (entity local positions line up with sim space).
/// Listens for <see cref="RoundEnterEvent"/> — no scene-manager wiring required.
/// Early execution so subscription is live before lab/session bootstrap raises the event.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(RectTransform))]
public class SimulationRectRenderer : MonoBehaviour
{
	RectTransform _rect;

	void Awake()
	{
		_rect = (RectTransform)transform;
	}

	void OnEnable()
	{
		SharedGameEventBus.Bus.SubscribeTo<RoundEnterEvent>(OnRoundEnter);
	}

	void OnDisable()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<RoundEnterEvent>(OnRoundEnter);
	}

	void OnRoundEnter(ref RoundEnterEvent @event)
	{
		SyncFrom(@event.playfield);
	}

	/// <summary>
	/// Makes this zone's local rect identical to <paramref name="playfield"/>
	/// (x: 0..width, y: ±height/2). Pivot matches that space; anchoredPosition puts
	/// <see cref="Rect.center"/> on the parent anchor so a left pivot does not shift the visual.
	/// </summary>
	public void SyncFrom(Rect playfield)
	{
		if (_rect == null)
			_rect = (RectTransform)transform;

		float w = playfield.width;
		float h = playfield.height;
		Vector2 pivot = new Vector2(
			w > 0f ? -playfield.xMin / w : 0.5f,
			h > 0f ? -playfield.yMin / h : 0.5f);

		// Size must be driven by sim data, not parent stretch layout.
		_rect.anchorMin = new Vector2(0.5f, 0.5f);
		_rect.anchorMax = new Vector2(0.5f, 0.5f);
		_rect.pivot = pivot;
		_rect.sizeDelta = new Vector2(w, h);
		// Pivot is at local (0,0); offset so playfield.center sits on the parent center anchor.
		_rect.anchoredPosition = -playfield.center;
	}
}
