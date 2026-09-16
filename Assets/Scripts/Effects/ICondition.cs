using System.Collections.Generic;
using BridgeOfBlood.Telemetry;

namespace BridgeOfBlood.Effects
{
	public interface ICondition
	{
		bool Evaluate(EffectContext context);
		string GetDescription();

		void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) { }
	}
}
