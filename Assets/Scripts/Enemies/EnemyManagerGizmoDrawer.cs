using BridgeOfBlood.Data.Enemies;
using UnityEngine;

/// <summary>
/// Draws gizmo spheres at each enemy position. Implements IDebugDrawable for use in TestSceneManager.
/// </summary>
public class EnemyManagerGizmoDrawer : IDebugDrawable
{
    private readonly EnemyManager _manager;
    private readonly float _radius;

    public EnemyManagerGizmoDrawer(EnemyManager manager, float radius = 5f)
    {
        _manager = manager;
        _radius = radius;
    }

    public void DrawGizmos(Transform transform)
    {
        if (_manager == null || transform == null) return;
        EnemyBuffers enemies = _manager.GetBuffers();
        if (enemies.Length == 0) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < enemies.Length; i++)
        {
            var m = enemies.Motion[i];
            Vector3 localPos = new Vector3(m.position.x, m.position.y, 0f);
            Vector3 worldPos = transform.TransformPoint(localPos);
            Gizmos.DrawSphere(worldPos, _radius);
        }
    }
}
