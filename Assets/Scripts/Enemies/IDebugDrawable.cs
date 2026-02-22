using UnityEngine;

/// <summary>
/// Implemented by systems that can draw debug visuals in the Scene view.
/// TestSceneManager calls DrawGizmos on all registered drawables in OnDrawGizmos.
/// </summary>
public interface IDebugDrawable
{
    void DrawGizmos(Transform transform);
}
