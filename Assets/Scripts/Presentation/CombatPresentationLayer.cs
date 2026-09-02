using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Single facade for combat-scene presentation: damage numbers, hit/kill effect sprites,
/// atlas-instanced sprite draw, attack hitbox debug overlay, and player transform sync.
/// Owned by <see cref="CombatPresentationDriver"/> — simulation never constructs or calls this.
/// </summary>
public sealed class CombatPresentationLayer
{
	readonly DamageNumberController _damageNumbers;
	readonly EffectSpriteController _effectSprites;
	readonly SpriteInstanceBuilder _spriteBuilder;
	readonly SpriteInstancedRenderer _spriteRenderer;
	readonly AttackEntityDebugRenderer _attackDebugRenderer;

	PlayerRenderer _playerRenderer;

	public CombatPresentationLayer(CombatPresentationResources resources)
	{
		_spriteRenderer = new SpriteInstancedRenderer(resources.spriteMaterial);
		_spriteBuilder = new SpriteInstanceBuilder(resources.spriteRenderDatabase);
		_damageNumbers = new DamageNumberController(resources.damageNumberMaterial);
		_effectSprites = new EffectSpriteController();
		_attackDebugRenderer = new AttackEntityDebugRenderer(resources.attackDebugMaterial);
	}

	public void BindAttackEntities(AttackEntityManager attackEntityManager)
	{
		_attackDebugRenderer.Bind(attackEntityManager);
	}

	public void BindPlayerRenderer(PlayerRenderer renderer)
	{
		_playerRenderer = renderer;
	}

	/// <summary>
	/// Spawns frame VFX, advances presentation clocks when sim time advanced, then draws.
	/// Called from the presentation driver during <see cref="SimulationCompleteEvent"/>
	/// (before transient combat buffers are cleared).
	/// </summary>
	public void HandleFrameComplete(
		ref SimulationCompleteEvent @event,
		RectTransform simulationZone,
		Camera camera)
	{
		GameSimulation.SimulationState sim = @event.simulationState;
		_damageNumbers.SpawnFromDamageEvents(sim.DamageEvents, sim.EnemyBuffers);
		_damageNumbers.SpawnFromTickDamageEvents(sim.TickDamageEvents, sim.EnemyBuffers);
		_effectSprites.SpawnFromDamageEvents(sim.DamageEvents);

		if (@event.simulationAdvanced)
		{
			_damageNumbers.Update(@event.deltaTime);
			_effectSprites.Update(@event.deltaTime);
		}

		SyncPlayerTransform(@event.playerPosition);
		_spriteBuilder.Build(sim.EnemyBuffers, sim.AttackEntities, _effectSprites.GetEntities());
		_spriteRenderer.Render(_spriteBuilder.Buffer, _spriteBuilder.Count, simulationZone, camera);
		_attackDebugRenderer.Render(sim.AttackEntities, sim.AttackHitBoxes, simulationZone, camera);
		_damageNumbers.Render(simulationZone, camera);
	}

	public void DrawGizmos(Transform simulationZone)
	{
		_attackDebugRenderer.DrawGizmos(simulationZone);
	}

	public void Dispose()
	{
		_spriteRenderer?.Dispose();
		_damageNumbers?.Dispose();
		_effectSprites?.Dispose();
		_attackDebugRenderer?.Dispose();
	}

	void SyncPlayerTransform(float2 position)
	{
		if (_playerRenderer == null)
			return;
		_playerRenderer.SyncTransform(position);
	}
}
