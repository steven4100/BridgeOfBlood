namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Per-spell identity for a telemetry window. Values are looked up by key on the aggregator.
	/// </summary>
	public struct SpellTelemetrySnapshot
	{
		public int spellId;
		public int invocationCount;
	}

	public struct FrameSnapshot
	{
		public float deltaTime;
		public float simulationTime;
	}

	public struct SpellCastSnapshot
	{
		public int spellId;
		public int invocationId;
	}

	public struct SpellLoopSnapshot
	{
		public int loopIndex;
		public SpellTelemetrySnapshot[] perSpell;
	}

	public struct RoundSnapshot
	{
		public int roundNumber;
		public int loopsCompleted;
		public SpellTelemetrySnapshot[] perSpell;
	}

	public struct GameSnapshot
	{
		public int roundsCompleted;
		public SpellTelemetrySnapshot[] perSpell;
	}
}
