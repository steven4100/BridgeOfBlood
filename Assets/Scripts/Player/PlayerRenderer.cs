using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour bridge that syncs its transform to the player's simulation position.
/// Place as a child of the simulation zone RectTransform so local position matches rect-local space.
/// Driven by <see cref="CombatPresentationLayer"/> each render pass so player presentation runs
/// in the same step as other combat draws (no separate LateUpdate timing).
/// </summary>
public class PlayerRenderer : MonoBehaviour
{
    public Player Player { get; set; }

    public void SyncTransform()
    {
        if (Player == null) return;

        float2 p = Player.Position;
        transform.localPosition = new Vector3(p.x, p.y, 0f);
    }
}
