using System;
using System.Threading;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using System.Collections.Generic;

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
	public sealed class RuntimeSpell
	{
		static int _nextSpellInstanceId;

		public SpellAuthoringData Definition { get; }

		/// <summary>Unique for each <see cref="RuntimeSpell"/> instance (including two slots sharing the same <see cref="Definition"/>).</summary>
		public readonly int spellId;
		public int InvocationCount { get; private set; }
		public double RoundTimeInvokedAt { get; private set; }
		public int numGemSlots;

		/// <summary>True while this slot is the next one to cast.</summary>
		public bool IsOnDeck { get; private set; }

		/// <summary>Raised when a cast is recorded (see <see cref="RecordCast"/>).</summary>
		public event Action CastInvoked;

		/// <summary>Raised when <see cref="IsOnDeck"/> flips.</summary>
		public event Action OnDeckChanged;


		//ToDo   a spell should not be aware of the items associated with it  
		public List<RuntimeSpellItem> spellItems = new List<RuntimeSpellItem>();


		public RuntimeSpell(SpellAuthoringData definition)
		{
			Definition = definition;
			spellId = Interlocked.Increment(ref _nextSpellInstanceId);
			InvocationCount = 0;
			RoundTimeInvokedAt = 0;
			numGemSlots = 2;
		}

		public void AddRuntimeSpellItem(RuntimeSpellItem spellItem)
		{
			spellItems.Add(spellItem);
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

	public class RuntimeSpellItem
	{
		public SpellItem spellItem;
	}

	/// <summary>
	/// Spell modifier applied during the modification pipeline.
	/// Follows the scaling philosophy: Flat → Additive → Conditional → Global → Cross-Spell
	///
	/// Note: Conditional evaluation happens upstream (in idol/effect systems).
	/// If a SpellModifier exists, it should be applied - predicates are resolved before modifier creation.
	///
	/// Spell attributes are immutable at runtime - they define the spell's identity and are set during authoring.
	/// </summary>
	public struct SpellModifier
	{
		public SpellAttributeMask targetAttributeMask;

		public int flatDamageBonus;
		public float damageMultiplier;
		public float castTimeMultiplier;
	}
}
