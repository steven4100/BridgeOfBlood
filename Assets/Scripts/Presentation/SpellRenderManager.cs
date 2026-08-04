using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Owns the lifecycle of diegetic <see cref="SpellRenderer"/> instances: instantiates one per loop slot from
/// <see cref="SpellAuthoringData.rendererPrefab"/>, binds it to that slot's <see cref="RuntimeSpell"/>, and
/// keeps instance order in sync with the loop.
///
/// Routes no forecast data: each bound renderer subscribes to its own spell's change events. The one thing this
/// pushes per frame is the player position from <see cref="SimulationCompleteEvent"/>, since every cast origin is
/// player-relative and the renderers place themselves against it.
///
/// <see cref="rendererRoot"/> must be the simulation zone rect so instance local space is simulation space.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class SpellRenderManager : MonoBehaviour
{
	[SerializeField] Transform rendererRoot;

	ISpellInventoryService _service;

	readonly Dictionary<int, SpellRenderer> _renderersBySpellId = new Dictionary<int, SpellRenderer>();
	readonly List<SpellRenderer> _boundRenderers = new List<SpellRenderer>();
	readonly List<int> _staleSpellIds = new List<int>();

	void OnEnable()
	{
		SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
	}

	void OnDisable()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
		if (_service != null)
		{
			_service.SpellsUpdated -= OnSpellsUpdated;
			_service = null;
		}
	}

	void OnDestroy()
	{
		DestroyAllRenderers();
	}

	/// <summary>Resolves <see cref="ISpellInventoryService"/> from <see cref="ServiceLocator.Current"/>.</summary>
	public void Initialize()
	{
		Initialize(ServiceLocator.Current.GetService<ISpellInventoryService>());
	}

	public void Initialize(ISpellInventoryService service)
	{
		if (ReferenceEquals(_service, service))
			return;

		if (_service != null)
			_service.SpellsUpdated -= OnSpellsUpdated;

		_service = service;
		_service.SpellsUpdated += OnSpellsUpdated;

		OnSpellsUpdated();
	}

	void OnServicesRegistered(ref ServicesRegisteredEvent _)
	{
		Initialize();
	}

	/// <summary>Keeps every renderer on the player, the origin all cast emissions are offset from.</summary>
	void OnSimulationComplete(ref SimulationCompleteEvent @event)
	{
		var playerPosition = new Vector2(@event.playerPosition.x, @event.playerPosition.y);
		for (int i = 0; i < _boundRenderers.Count; i++)
			_boundRenderers[i].SyncOrigin(playerPosition);
	}

	void OnSpellsUpdated()
	{
		IReadOnlyList<RuntimeSpell> spells = _service.GetSpells();

		_staleSpellIds.Clear();
		foreach (int spellId in _renderersBySpellId.Keys)
		{
			if (!ContainsSpellId(spells, spellId))
				_staleSpellIds.Add(spellId);
		}

		for (int i = 0; i < _staleSpellIds.Count; i++)
			DestroyRenderer(_staleSpellIds[i]);

		_boundRenderers.Clear();
		int siblingIndex = 0;
		for (int i = 0; i < spells.Count; i++)
		{
			RuntimeSpell spell = spells[i];
			SpellRenderer renderer = ResolveRenderer(spell);
			if (renderer == null)
				continue;

			renderer.transform.SetSiblingIndex(siblingIndex++);
			_boundRenderers.Add(renderer);
		}
	}

	/// <summary>Returns the instance bound to <paramref name="spell"/>, creating it on first use. Null when the spell has no renderer prefab.</summary>
	SpellRenderer ResolveRenderer(RuntimeSpell spell)
	{
		if (_renderersBySpellId.TryGetValue(spell.spellId, out SpellRenderer existing))
			return existing;

		SpellRenderer prefab = spell.Definition.rendererPrefab;
		if (prefab == null)
			return null;

		SpellRenderer instance = Instantiate(prefab, rendererRoot);
		instance.Bind(spell);
		_renderersBySpellId.Add(spell.spellId, instance);
		return instance;
	}

	void DestroyRenderer(int spellId)
	{
		if (!_renderersBySpellId.TryGetValue(spellId, out SpellRenderer renderer))
			return;

		_renderersBySpellId.Remove(spellId);
		_boundRenderers.Remove(renderer);
		renderer.Unbind();
		Destroy(renderer.gameObject);
	}

	void DestroyAllRenderers()
	{
		foreach (SpellRenderer renderer in _renderersBySpellId.Values)
		{
			renderer.Unbind();
			Destroy(renderer.gameObject);
		}
		_renderersBySpellId.Clear();
		_boundRenderers.Clear();
	}

	static bool ContainsSpellId(IReadOnlyList<RuntimeSpell> spells, int spellId)
	{
		for (int i = 0; i < spells.Count; i++)
		{
			if (spells[i].spellId == spellId)
				return true;
		}
		return false;
	}
}
