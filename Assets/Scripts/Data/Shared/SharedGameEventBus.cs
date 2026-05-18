using GenericEventBus;

namespace BridgeOfBlood.Data.Shared
{
	public static class SharedGameEventBus
	{
		public static readonly GenericEventBus<IEvent> Bus = new GenericEventBus<IEvent>();
	}

	public interface IEvent { }
}
