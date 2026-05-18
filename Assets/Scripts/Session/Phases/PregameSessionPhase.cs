using UnityEngine;

public sealed class PregameSessionPhase : SessionPhaseBase
{
	public PregameSessionPhase(SessionFlowController controller) : base(controller) { }

    public override void Tick(SessionFlowContext context, float deltaTime)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            context.Flow.SetState(SessionState.Round);
    }

    protected override void OnEnter(SessionFlowContext context)
    {
        
    }

    protected override void OnExit(SessionFlowContext context)
    {
        throw new System.NotImplementedException();
    }

}
