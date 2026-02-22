using UnityEngine;

/// <summary>
/// Debug controller for simulation time flow.
/// Supports play, pause, step-frame (all phases at once), and step-into (one phase at a time).
/// Attach alongside TestSceneManager. When no controller is present, simulation runs normally.
/// </summary>
public class SimulationDebugController : MonoBehaviour
{
    public enum PlayState { Playing, Paused }

    [Header("Key Bindings")]
    public KeyCode togglePlayPauseKey = KeyCode.Space;
    public KeyCode stepFrameKey = KeyCode.Period;
    public KeyCode stepPhaseKey = KeyCode.Comma;

    [Header("Stepping")]
    [Tooltip("Delta time used when stepping (play mode uses Time.deltaTime)")]
    public float stepDeltaTime = 1f / 60f;

    [Header("State (read-only)")]
    [SerializeField] private PlayState _playState = PlayState.Paused;
    [SerializeField] private int _currentPhaseIndex;
    [SerializeField] private string _currentPhaseName = "";
    [SerializeField] private int _simulationFrameCount;

    private int _totalPhases;
    private int _pendingSteps;

    public PlayState State => _playState;
    public int CurrentPhaseIndex => _currentPhaseIndex;
    public string CurrentPhaseName => _currentPhaseName;
    public int SimulationFrameCount => _simulationFrameCount;

    /// <summary>
    /// The delta time the simulation should use this frame.
    /// Returns Time.deltaTime when playing, stepDeltaTime when stepping.
    /// </summary>
    public float DeltaTime => _playState == PlayState.Playing ? Time.deltaTime : stepDeltaTime;

    public void Initialize(int totalPhases)
    {
        _totalPhases = totalPhases;
        _currentPhaseIndex = 0;
    }

    /// <summary>
    /// Call at the top of Update before any phase checks.
    /// </summary>
    public void ProcessInput()
    {
        if (Input.GetKeyDown(togglePlayPauseKey))
        {
            _playState = _playState == PlayState.Playing ? PlayState.Paused : PlayState.Playing;
            if (_playState == PlayState.Playing)
                _currentPhaseIndex = 0;
            _pendingSteps = 0;
        }

        if (_playState != PlayState.Paused) return;

        if (Input.GetKeyDown(stepFrameKey))
            _pendingSteps += _totalPhases - _currentPhaseIndex;
        else if (Input.GetKeyDown(stepPhaseKey))
            _pendingSteps++;
    }

    /// <summary>
    /// True if the simulation should advance time this frame (beginning of a new sim frame).
    /// Time advances when playing, or when paused at phase 0 with pending steps.
    /// </summary>
    public bool ShouldAdvanceTime =>
        _playState == PlayState.Playing || (_currentPhaseIndex == 0 && _pendingSteps > 0);

    /// <summary>
    /// Call once per phase in order (phaseIndex 0, 1, 2...).
    /// Returns true if the phase should execute.
    /// </summary>
    public bool ShouldRunPhase(int phaseIndex, string phaseName)
    {
        if (_playState == PlayState.Playing)
        {
            _currentPhaseName = phaseName;
            return true;
        }

        if (phaseIndex != _currentPhaseIndex || _pendingSteps <= 0)
            return false;

        _pendingSteps--;
        _currentPhaseName = phaseName;
        _currentPhaseIndex++;

        if (_currentPhaseIndex >= _totalPhases)
        {
            _currentPhaseIndex = 0;
            _simulationFrameCount++;
        }

        return true;
    }

    /// <summary>
    /// Call at the end of Update when playing to keep frame count in sync.
    /// </summary>
    public void NotifyFrameComplete()
    {
        if (_playState == PlayState.Playing)
            _simulationFrameCount++;
    }
}
