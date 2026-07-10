using BridgeOfBlood.Data.Shared;
using UnityEngine;

/// <summary>
/// Single facade for combat-scene presentation: damage numbers, hit/kill effect sprites,
/// atlas-instanced sprite draw, attack hitbox debug overlay, and player transform sync.
/// <para>
/// Owns the lifetime of every visual subsystem so <see cref="RoundController"/> and the scene
/// bootstrap only depend on this one object. The simulation boundary is preserved: presentation
/// reads <see cref="GameSimulation.SimulationState"/> and frame event lists, never simulation
/// internals or <see cref="AttackEntityManager"/> directly (the debug renderer takes a manager
/// reference at construction for its own gizmo path).
/// </para>
/// </summary>
public sealed class CombatPresentationLayer
{
	readonly DamageNumberController _damageNumbers;
	readonly EffectSpriteController _effectSprites;
	readonly SpriteInstanceBuilder _spriteBuilder;
	readonly SpriteInstancedRenderer _spriteRenderer;
	readonly AttackEntityDebugRenderer _attackDebugRenderer;

	PlayerRenderer _playerRenderer;
	Player _player;

	public CombatPresentationLayer(
		CombatPresentationResources resources,
		AttackEntityManager attackEntityManager)
	{
		_spriteRenderer = new SpriteInstancedRenderer(resources.spriteMaterial);
		_spriteBuilder = new SpriteInstanceBuilder(resources.spriteRenderDatabase);
		_damageNumbers = new DamageNumberController(resources.damageNumberMaterial);
		_effectSprites = new EffectSpriteController();
		_attackDebugRenderer = new AttackEntityDebugRenderer(attackEntityManager, resources.attackDebugMaterial);
		SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
	}

	/// <summary>
	/// Wires the optional <see cref="PlayerRenderer"/> so the player transform follows simulation
	/// position in the same pass as combat draws (no separate LateUpdate).
	/// </summary>
	public void BindPlayer(PlayerRenderer renderer, Player player)
	{
		_playerRenderer = renderer;
		_player = player;
		if (_playerRenderer != null)
			_playerRenderer.Player = player;
	}

	/// <summary>
	/// Spawns damage numbers and effect sprites for this frame's events.
	/// </summary>
	void OnSimulationComplete(ref SimulationCompleteEvent @event)
	{
		GameSimulation.SimulationState sim = @event.simulationState;
		_damageNumbers.SpawnFromDamageEvents(sim.DamageEvents, sim.EnemyBuffers);
		_damageNumbers.SpawnFromTickDamageEvents(sim.TickDamageEvents, sim.EnemyBuffers);
		_effectSprites.SpawnFromDamageEvents(sim.DamageEvents);
	}

	/// <summary>Advances damage number motion and effect sprite lifetimes. Skip when sim time is paused.</summary>
	public void Update(float deltaTime)
	{
		_damageNumbers.Update(deltaTime);
		_effectSprites.Update(deltaTime);
	}

	/// <summary>Draws all combat visuals for this frame (sprites, hitbox debug, damage numbers) and syncs the player transform.</summary>
	public void Render(GameSimulation.SimulationState sim, RectTransform simulationZone, Camera camera)
	{
		SyncPlayerTransform();
		_spriteBuilder.Build(sim.EnemyBuffers, sim.AttackEntities, _effectSprites.GetEntities());
		_spriteRenderer.Render(_spriteBuilder.Buffer, _spriteBuilder.Count, simulationZone, camera);
		_attackDebugRenderer.Render(sim.AttackEntities, simulationZone, camera);
		_damageNumbers.Render(simulationZone, camera);
	}

	/// <summary>Forwards Scene-view gizmo drawing for attack hitboxes; called from <c>OnDrawGizmos</c>.</summary>
	public void DrawGizmos(Transform simulationZone)
	{
		_attackDebugRenderer.DrawGizmos(simulationZone);
	}

	public void Dispose()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
		_spriteRenderer?.Dispose();
		_damageNumbers?.Dispose();
		_effectSprites?.Dispose();
		_attackDebugRenderer?.Dispose();
	}

	void SyncPlayerTransform()
	{
		if (_playerRenderer == null || _player == null)
			return;
		_playerRenderer.SyncTransform();
	}
}
