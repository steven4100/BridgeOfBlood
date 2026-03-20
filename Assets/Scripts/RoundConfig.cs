using System;
using UnityEngine;

/// <summary>
/// Per-round configuration: blood quota the player must meet, and how many spell loops they get to do it.
/// Serializable so it can be embedded in a MonoBehaviour inspector (e.g. TestSceneManager).
/// </summary>
[Serializable]
public class RoundConfig
{
    [Tooltip("Total bloodExtracted the player must reach to pass the round.")]
    public float bloodQuota = 1000f;

    [Tooltip("Number of complete spell loops the player gets per round.")]
    public int spellLoopsPerRound = 3;
}
