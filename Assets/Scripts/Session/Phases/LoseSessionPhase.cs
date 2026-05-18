using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class LoseSessionPhase : SessionPhaseBase
{
	readonly Func<GameConfig> _createRuntimeGameConfigCopy;

	public LoseSessionPhase(SessionFlowController controller, Func<GameConfig> createRuntimeGameConfigCopy)
		: base(controller)
	{
		_createRuntimeGameConfigCopy = createRuntimeGameConfigCopy;
	}

    public override void Tick(SessionFlowContext context, float deltaTime)
    {
        if (!Input.GetKeyDown(KeyCode.R))
        {
            context.Flow.SetState(SessionState.Round);
            context.RuntimeGameConfig = _createRuntimeGameConfigCopy();
        }
    }

    protected override void OnEnter(SessionFlowContext context) { }

    protected override void OnExit(SessionFlowContext context) { }
}
