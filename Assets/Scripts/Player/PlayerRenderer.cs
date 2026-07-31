using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour bridge that syncs its transform to the player's simulation position.
/// Place as a child of the simulation zone RectTransform so local position matches rect-local space.
/// Driven by <see cref="CombatPresentationLayer"/> each frame-complete pass.
/// </summary>
public class PlayerRenderer : MonoBehaviour
{
    public void SyncTransform(float2 position)
    {
        transform.localPosition = new Vector3(position.x, position.y, 0f);
    }
}
