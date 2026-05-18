using GenericEventBus;
using System;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Non-generic root for <see cref="SessionPhaseEvent{T}"/> so <c>[SerializeReference]</c> can pick concrete payloads (inspector / assets).
	/// </summary>
	[Serializable]
	public abstract class SessionPhaseEventBase : IEvent
	{
		public SessionState state;
		public int roundNumber;

		public abstract void Invoke();
	}

	[Serializable]
	public abstract class SessionPhaseEvent<T> : SessionPhaseEventBase where T : SessionPhaseEvent<T>
	{
		public sealed override void Invoke()
		{
			var self = (T)this;
			SharedGameEventBus.Bus.Raise(in self);
		}
	}

	[Serializable]
	public sealed class RoundEnterEvent : SessionPhaseEvent<RoundEnterEvent>
	{
		public float bloodQuota;
		public int spellLoopsPerRound;
	}

	[Serializable]
	public sealed class RoundExitEvent : SessionPhaseEvent<RoundExitEvent> 
	{
		public float bloodExtracted;
		public bool quotaMet;
	}

	[Serializable]
	public sealed class ShopEnterEvent : SessionPhaseEvent<ShopEnterEvent> 
	{
		public int gold;
	}

	[Serializable]
	public sealed class ShopExitEvent : SessionPhaseEvent<ShopExitEvent> 
	{
		public int gold;
	}

	[Serializable]
	public sealed class ShopContinueButtonPressed : SessionPhaseEvent<ShopContinueButtonPressed>
	{
	}
}
