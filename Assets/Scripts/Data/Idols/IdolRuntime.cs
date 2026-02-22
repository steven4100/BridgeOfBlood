using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Idols
{
	/// <summary>
	/// Runtime predicate for evaluating conditions.
	/// Part of Event → Condition → Effect architecture.
	/// </summary>
	public struct Predicate
	{
		public int gamePropertyId; // Reference to game context property
		public Comparison comparison;
		public float threshold;
	}

	/// <summary>
	/// Runtime effect data. Struct-based for performance.
	/// Effects modify spells, stats, or game state based on conditions.
	/// Uses a discriminated union pattern to avoid wasting memory.
	/// </summary>
	public struct IdolEffect
	{
		public int effectId;
		public EffectType effectType;
		
		// Union: Only one of these is valid based on effectType
		// Using explicit layout to save memory
		private SpellModifierData _spellModifier;
		private StatModifierData _statModifier;
		private GameStateModifierData _gameStateModifier;

		public SpellModifierData SpellModifier
		{
			get => _spellModifier;
			set => _spellModifier = value;
		}

		public StatModifierData StatModifier
		{
			get => _statModifier;
			set => _statModifier = value;
		}

		public GameStateModifierData GameStateModifier
		{
			get => _gameStateModifier;
			set => _gameStateModifier = value;
		}
	}

	/// <summary>
	/// Types of effects that idols can apply.
	/// </summary>
	public enum EffectType : byte
	{
		SpellModifier = 0,
		StatModifier = 1,
		GameStateModifier = 2
	}

	/// <summary>
	/// Spell modifier data embedded in effects.
	/// Converts to SpellModifier for application.
	/// </summary>
	public struct SpellModifierData
	{
		public SpellAttributeMask targetAttributeMask;
		public int flatValue;
		public float multiplier;
		public float castTimeMultiplier;
	}

	/// <summary>
	/// Stat modifier data embedded in effects.
	/// </summary>
	public struct StatModifierData
	{
		public int statId;
		public Operation operation;
		public int flatValue;
		public float multiplier;
	}

	/// <summary>
	/// Game state modifier data embedded in effects.
	/// </summary>
	public struct GameStateModifierData
	{
		public int gameStatePropertyId;
		public Operation operation;
		public float value;
	}
}
