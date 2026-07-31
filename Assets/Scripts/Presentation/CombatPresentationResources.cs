using System;
using UnityEngine;

/// <summary>
/// Materials and sprite atlas database required to build a <see cref="CombatPresentationLayer"/>.
/// Serialized on <see cref="CombatPresentationDriver"/> (prefab/scene) — not on GameConfig.
/// </summary>
[Serializable]
public sealed class CombatPresentationResources
{
	public Material spriteMaterial;
	public Material damageNumberMaterial;
	public Material attackDebugMaterial;
	public SpriteRenderDatabase spriteRenderDatabase;
}
