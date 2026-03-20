/// <summary>
/// High-level session states. Controls which systems run each frame.
/// </summary>
public enum SessionState
{
    Pregame,
    Round,
    Shop,
    Lose
}

/// <summary>
/// Lightweight state machine that brokers transitions between session phases.
/// No Unity or simulation references; the runner reads CurrentState and performs
/// the actual setup/teardown work when transitions occur.
/// </summary>
public class SessionStateMachine
{
    public SessionState CurrentState { get; private set; }

    public SessionStateMachine()
    {
        CurrentState = SessionState.Pregame;
    }

    /// <summary>Pregame -> Round. Returns true if the transition occurred.</summary>
    public bool RequestStart()
    {
        if (CurrentState != SessionState.Pregame) return false;
        CurrentState = SessionState.Round;
        return true;
    }

    /// <summary>Round -> Shop (quota met) or Lose (quota not met). Returns true if the transition occurred.</summary>
    public bool OnRoundEnded(bool quotaMet)
    {
        if (CurrentState != SessionState.Round) return false;
        CurrentState = quotaMet ? SessionState.Shop : SessionState.Lose;
        return true;
    }

    /// <summary>Shop -> Round. Returns true if the transition occurred.</summary>
    public bool RequestNextRound()
    {
        if (CurrentState != SessionState.Shop) return false;
        CurrentState = SessionState.Round;
        return true;
    }

    /// <summary>Lose -> Round. Returns true if the transition occurred.</summary>
    public bool RequestRetry()
    {
        if (CurrentState != SessionState.Lose) return false;
        CurrentState = SessionState.Round;
        return true;
    }
}
