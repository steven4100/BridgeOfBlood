using System;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Types of damage that spells can deal.
	/// </summary>
	public enum DamageType : byte
	{
		Physical = 0,
		Cold = 1,
		Fire = 2,
		Lightning = 3,
		None = 4
	}

	/// <summary>
	/// Flags representing spell attributes and behaviors.
	/// Used for tag-based synergies and conditional modifiers.
	/// </summary>
	[Flags]
	public enum SpellAttributeMask : ushort
	{
		None = 0,
		AOE = 1 << 0,
		Chains = 1 << 1,
		Physical = 1 << 2,
		Fire = 1 << 3,
		Cold = 1 << 4,
		Lightning = 1 << 5
	}

	/// <summary>
	/// Status ailments that can be applied to enemies.
	/// </summary>
	[Flags]
	public enum StatusAilmentFlag : byte
	{
		None = 0,
		Frozen = 1 << 0,
		Stunned = 1 << 1,
		Poisoned = 1 << 2,
		Ignited = 1 << 3,
		Shocked = 1 << 4
	}

	/// <summary>
	/// Corruption types representing enemy characteristics.
	/// Used for conditional modifiers and synergies.
	/// </summary>
	[Flags]
	public enum EnemyCorruptionFlag : byte
	{
		None = 0,
		Gluttonous = 1 << 0,
		Envious = 1 << 1,
		Lustful = 1 << 2,
		Greedy = 1 << 3,
		Sloth = 1 << 4,
		Prideful = 1 << 5,
		Wrathful = 1 << 6
	}

	/// <summary>
	/// Comparison operations for predicates.
	/// </summary>
	public enum Comparison : byte
	{
		GreaterThan = 0,
		LessThan = 1,
		Equal = 2,
		NotEqual = 3,
		GreaterThanOrEqual = 4,
		LessThanOrEqual = 5
	}

	/// <summary>
	/// Operations for applying modifiers.
	/// </summary>
	public enum Operation : byte
	{
		Add = 0,
		Multiply = 1
	}

	/// <summary>
	/// Types of events that systems can react to.
	/// Based on combat telemetry requirements.
	/// Used by idols and other game modification systems.
	/// </summary>
	public enum GameEventType : byte
	{
		EnemyHit = 0,
		EnemyKilled = 1,
		SpellInvoked = 2,
		StatusAilmentApplied = 3,
		StatusAilmentConsumed = 4,
		KillStreak = 5,
		OverkillThreshold = 6,
		SpellLoopCompleted = 7
	}

	public enum Rarity : byte
	{
		Common = 0,
		Uncommon = 1,
		Rare = 2,
		Epic = 3,
		Legendary = 4
	}

	public enum ShopItemType : byte
	{
		Spell = 0,
		Joker = 1
	}
}

