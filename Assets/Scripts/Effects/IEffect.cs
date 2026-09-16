using System.Collections.Generic;
using BridgeOfBlood.Telemetry;

namespace BridgeOfBlood.Effects
{
	public interface IEffect
	{
		bool Apply(EffectContext context);
		string GetDescription();

		void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) { }
	}
}
