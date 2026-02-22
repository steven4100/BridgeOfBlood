using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Idols
{
	/// <summary>
	/// Authoring data for idols following Event → Condition → Effect architecture.
	/// Idols react to combat telemetry and apply modifiers based on conditions.
	/// </summary>
	[CreateAssetMenu(fileName = "IdolData", menuName = "BridgeOfBlood/Idols/Idol Authoring Data")]
	public class IdolAuthoringData : ScriptableObject
	{
		[Header("Event")]
		[Tooltip("Which event triggers this idol's condition evaluation")]
		public Shared.GameEventType eventType;

		[Header("Condition")]
		[Tooltip("Predicate that must be true for the effect to apply")]
		public PredicateAuthoring predicate;

		[Header("Effect")]
		[Tooltip("Effect to apply when condition is met")]
		public EffectAuthoring effect;
	}

	/// <summary>
	/// Authoring data for predicates.
	/// </summary>
	[System.Serializable]
	public class PredicateAuthoring
	{
		[Tooltip("Game context property to evaluate")]
		public GameContextProperty gameProperty;

		[Tooltip("Comparison operation")]
		public Comparison comparison;

		[Tooltip("Threshold value for comparison")]
		public float threshold;
	}

	/// <summary>
	/// Authoring data for effects.
	/// </summary>
	[System.Serializable]
	public class EffectAuthoring
	{
		[Tooltip("Type of effect")]
		public EffectType effectType;

		[Header("Spell Modifier (if effectType is SpellModifier)")]
		public SpellAttributeMask targetAttributeMask;
		public Operation operation;
		public int flatValue;
		public float multiplier;

		[Header("Stat Modifier (if effectType is StatModifier)")]
		public int statId;
		public int statFlatValue;
		public float statMultiplier;

		[Header("Game State Modifier (if effectType is GameStateModifier)")]
		public int gameStatePropertyId;
		public float gameStateValue;
	}

	/// <summary>
	/// Reference to a game context property for predicates.
	/// </summary>
	[System.Serializable]
	public class GameContextProperty
	{
		[Tooltip("Name/ID of the game property field")]
		public string propertyName;

		[Tooltip("Property type identifier")]
		public int propertyId;
	}
}

