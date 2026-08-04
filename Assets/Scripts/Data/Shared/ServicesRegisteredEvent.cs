using GenericEventBus;
using UnityEngine;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Raised after session bootstrap (or reset) finishes registering services on
	/// <see cref="EZServiceLocation.ServiceLocator"/>. Consumers should resolve and cache
	/// service instances in response to this event — not from <c>Start</c> / ad-hoc timing.
	///
	/// Sticky: the most recent raise is kept in <see cref="LastRaised"/>, so objects created or
	/// enabled after bootstrap can catch up through <see cref="SubscribeAndCatchUp"/> rather than
	/// waiting for a registration pass that may never happen again.
	/// </summary>
	public struct ServicesRegisteredEvent : IEvent
	{
		/// <summary>Most recent raise, or null while services have not been registered yet this play session.</summary>
		public static ServicesRegisteredEvent? LastRaised { get; private set; }

		/// <summary>Records the raise for late subscribers, then raises it on <see cref="SharedGameEventBus.Bus"/>.</summary>
		public static void Raise()
		{
			var @event = new ServicesRegisteredEvent();
			LastRaised = @event;
			SharedGameEventBus.Bus.Raise(@event);
		}

		/// <summary>
		/// Subscribes <paramref name="handler"/> and immediately replays <see cref="LastRaised"/> when services are
		/// already registered, so binding does not depend on this object existing before bootstrap.
		/// </summary>
		public static void SubscribeAndCatchUp(GenericEventBus<IEvent>.EventHandler<ServicesRegisteredEvent> handler)
		{
			SharedGameEventBus.Bus.SubscribeTo(handler);

			if (!LastRaised.HasValue)
				return;

			ServicesRegisteredEvent replay = LastRaised.Value;
			handler(ref replay);
		}

		/// <summary>Pairs with <see cref="SubscribeAndCatchUp"/>.</summary>
		public static void Unsubscribe(GenericEventBus<IEvent>.EventHandler<ServicesRegisteredEvent> handler)
		{
			SharedGameEventBus.Bus.UnsubscribeFrom(handler);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetLastRaised()
		{
			LastRaised = null;
		}
	}
}
