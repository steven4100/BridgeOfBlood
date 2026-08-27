namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Snapshot of game state produced once per frame by the simulation runner.
	/// This is the only data the UI should read from. Filled after the frame completes.
	/// </summary>
	public struct GameState
	{
		/// <summary>High-level session state (Pregame, Round, Shop, Lose).</summary>
		public SessionState sessionState;

		/// <summary>Current phase of the round-based game loop.</summary>
		public GameLoopPhase phase;

		/// <summary>1-based round index.</summary>
		public int roundNumber;

		/// <summary>Authored kill quota as a percent of enemies spawned this round.</summary>
		public float killQuotaPercent;

		/// <summary>Resolved kill count required this round (from quota percent × spawned).</summary>
		public int minEnemiesKilled;

		/// <summary>Enemies created this round (including already killed).</summary>
		public int enemiesSpawnedThisRound;

		/// <summary>Blood extracted so far this round.</summary>
		public float bloodExtracted;

		/// <summary>True when the round ended and the player met the quota.</summary>
		public bool quotaMet;

		/// <summary>Number of spell loops allowed this round.</summary>
		public int spellLoopsPerRound;

		/// <summary>Number of complete spell loops completed so far this round.</summary>
		public int loopsCompleted;

		/// <summary>Combat metrics accumulated this round (hits, kills, damage, blood, etc.).</summary>
		public CombatMetrics roundMetrics;

		/// <summary>Current simulation time in seconds.</summary>
		public float simulationTime;

		/// <summary>Number of live enemies (optional, for debug or HUD).</summary>
		public int enemyCount;

		/// <summary>Number of live attack entities (optional, for debug or "projectiles in flight").</summary>
		public int attackEntityCount;
	}
}
