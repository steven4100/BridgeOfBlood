using System;
using System.Threading;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Per-loop-slot runtime state. Static spell data is read from <see cref="Definition"/>; only invocation
	/// counters and on-deck state mutate during play.
	///
	/// This type owns the events that signal changes to its own data: mutation goes through methods
	/// (<see cref="RecordCast"/>, <see cref="SetOnDeck"/>) so notifications cannot be skipped.
	/// Only <see cref="LoopedSpellCaster"/> calls them; presentation subscribes and reads.
	/// </summary>
	public sealed class RuntimeSpell : IInventoryOccupant
	{
		static int _nextSpellInstanceId;

		public SpellAuthoringData Definition { get; }

		/// <summary>Unique for each <see cref="RuntimeSpell"/> instance (including two slots sharing the same <see cref="Definition"/>).</summary>
		public readonly int spellId;
		public int InvocationCount { get; private set; }
		public double RoundTimeInvokedAt { get; private set; }

		public RuntimeSpellGemCollection Gems { get; }

		/// <summary>True while this slot is the next one to cast.</summary>
		public bool IsOnDeck { get; private set; }

		/// <summary>Raised when a cast is recorded (see <see cref="RecordCast"/>).</summary>
		public event Action CastInvoked;

		/// <summary>Raised when <see cref="IsOnDeck"/> flips.</summary>
		public event Action OnDeckChanged;

		public event Action GemsChanged;

		public int OccupancyCount => 1 + Gems.FilledCount;
		public int TileSideLength => 1;
		public Sprite GhostSprite => Definition.icon;

		public RuntimeSpell(SpellAuthoringData definition)
		{
			Definition = definition;
			spellId = Interlocked.Increment(ref _nextSpellInstanceId);
			InvocationCount = 0;
			RoundTimeInvokedAt = 0;
			Gems = new RuntimeSpellGemCollection(this, 2, () => GemsChanged?.Invoke());
		}

		/// <summary>
		/// Stamps the cast time, increments the invocation counter, then raises <see cref="CastInvoked"/>.
		/// </summary>
		public void RecordCast(double roundTime)
		{
			RoundTimeInvokedAt = roundTime;
			InvocationCount++;
			CastInvoked?.Invoke();
		}

		/// <summary>
		/// Sets <see cref="IsOnDeck"/> and raises <see cref="OnDeckChanged"/> if it flipped.
		/// Returns whether anything changed.
		/// </summary>
		public bool SetOnDeck(bool onDeck)
		{
			if (IsOnDeck == onDeck)
				return false;

			IsOnDeck = onDeck;
			OnDeckChanged?.Invoke();
			return true;
		}

		/// <summary>Clears per-round cast tracking (invocation counter and cast timestamp).</summary>
		public void ResetTracking()
		{
			InvocationCount = 0;
			RoundTimeInvokedAt = 0;
		}
	}

	/// <summary>
	/// Spell modifier applied during the modification pipeline.
	/// Follows the scaling philosophy: Flat → Additive → Conditional → Global → Cross-Spell
	///
	/// Note: Conditional evaluation happens upstream (in idol/effect systems).
	/// If a SpellModifier exists, it should be applied - predicates are resolved before modifier creation.
	/// </summary>
	public struct SpellModifier
	{
		public SpellAttributeMask targetAttributeMask;

		public int flatDamageBonus;
		public float damageMultiplier;
		public float castTimeMultiplier;
	}
}
