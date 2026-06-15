using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Single facade for all combat-scene presentation: damage numbers, hit/kill effect sprites,
/// combat audio, atlas-instanced sprite draw, attack hitbox debug overlay, and player transform sync.
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
	readonly GameAudioManager _audio;

	PlayerRenderer _playerRenderer;
	Player _player;

	public CombatPresentationLayer(
		CombatPresentationResources resources,
		GameAudioManager audio,
		AttackEntityManager attackEntityManager)
	{
		_spriteRenderer = new SpriteInstancedRenderer(resources.spriteMaterial);
		_spriteBuilder = new SpriteInstanceBuilder(resources.spriteRenderDatabase);
		_damageNumbers = new DamageNumberController(resources.damageNumberMaterial);
		_effectSprites = new EffectSpriteController();
		_attackDebugRenderer = new AttackEntityDebugRenderer(attackEntityManager, resources.attackDebugMaterial);
		_audio = audio;
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
	/// Spawns damage numbers + effect sprites and enqueues combat audio for this frame's events,
	/// then drains the audio queue so combat sounds play in the same frame as their visuals.
	/// </summary>
	public void ConsumeFrame(GameSimulation.SimulationState sim)
	{
		_damageNumbers.SpawnFromDamageEvents(sim.DamageEvents, sim.EnemyBuffers);
		_damageNumbers.SpawnFromTickDamageEvents(sim.TickDamageEvents, sim.EnemyBuffers);
		_effectSprites.SpawnFromDamageEvents(sim.DamageEvents);
		_audio.EnqueueFromCombatEvents(sim.DamageEvents, sim.KillEvents);
		_audio.UpdateDrain();
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

	/// <summary>
	/// Plays the cast audio bound to the spell that just emitted, using a deterministic seed so
	/// repeat casts of the same invocation produce the same pitch/volume roll.
	/// </summary>
	public void PlayCastAudio(in SpellCastResult castResult, IReadOnlyList<RuntimeSpell> spells, float2 origin)
	{
		if (!castResult.didCast)
			return;

		for (int i = 0; i < spells.Count; i++)
		{
			RuntimeSpell spell = spells[i];
			if (spell.spellId != castResult.spellId)
				continue;

			AudioUnit unit = spell.Definition.castAudio;
			if (unit != null)
			{
				uint seed = AttackEntityBuildRngSeed.Mix(
					castResult.spellId,
					castResult.invocationCount,
					0,
					0x41A3F5C);
				_audio.RequestOneShot(unit.ToRuntime(seed), origin);
			}
			return;
		}
	}

	/// <summary>Forwards Scene-view gizmo drawing for attack hitboxes; called from <c>OnDrawGizmos</c>.</summary>
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

	void SyncPlayerTransform()
	{
		if (_playerRenderer == null || _player == null)
			return;
		_playerRenderer.SyncTransform();
	}
}
