using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Simple MonoBehaviour that syncs its transform to the player's position each frame.
/// Place as a child of the simulation zone RectTransform so local position matches rect-local space.
/// Assign the Player reference at runtime (e.g. from TestSceneManager).
/// </summary>
public class PlayerRenderer : MonoBehaviour
{
    public Player Player { get; set; }

    void LateUpdate()
    {
        if (Player == null) return;

        float2 p = Player.Position;
        transform.localPosition = new Vector3(p.x, p.y, 0f);
    }
}
