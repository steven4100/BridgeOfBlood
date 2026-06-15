using UnityEngine;

/// <summary>
/// Maps screen pointers to simulation-zone local coordinates (enemy / player / spell space).
/// </summary>
public static class SimulationZonePointer
{
    public static bool TryGetLocalPoint(
        RectTransform zone,
        Camera sceneCamera,
        Vector2 screenPosition,
        out Vector2 localPoint)
    {
        localPoint = default;
        if (zone == null)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            zone,
            screenPosition,
            ResolveEventCamera(zone, sceneCamera),
            out localPoint);
    }

    /// <summary>
    /// Camera for UI raycast conversion: null for overlay canvas, otherwise world camera or scene camera.
    /// </summary>
    public static Camera ResolveEventCamera(RectTransform zone, Camera sceneCamera)
    {
        Canvas canvas = zone.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            if (canvas.worldCamera != null)
                return canvas.worldCamera;
        }

        return sceneCamera != null ? sceneCamera : Camera.main;
    }
}
