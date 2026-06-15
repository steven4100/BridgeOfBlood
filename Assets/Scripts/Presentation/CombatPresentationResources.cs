using System;
using UnityEngine;

/// <summary>
/// Authoring asset references required to build a <see cref="CombatPresentationLayer"/>.
/// Serialized on <see cref="BridgeOfBlood.Data.Shared.GameConfig"/> so combat visuals are
/// authored alongside other game data. Scene/runtime collaborators (the scene's
/// <see cref="GameAudioManager"/>, the simulation's <see cref="AttackEntityManager"/>) are
/// passed separately at layer construction since they cannot be referenced from a ScriptableObject.
/// </summary>
[Serializable]
public sealed class CombatPresentationResources
{
	public Material spriteMaterial;
	public Material damageNumberMaterial;
	public Material attackDebugMaterial;
	public SpriteRenderDatabase spriteRenderDatabase;
}
