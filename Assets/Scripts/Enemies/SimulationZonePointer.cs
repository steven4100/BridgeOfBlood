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

        Camera cam = ResolveEventCamera(zone, sceneCamera);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                zone,
                screenPosition,
                cam,
                out localPoint))
        {
            return true;
        }

        // Unity's helper raycasts one side of the rect plane. A 90° playfield with
        // negative Z (Lab / MapMod authoring) faces the camera visually but fails that test.
        return TryIntersectLocalPoint(zone, cam, screenPosition, out localPoint);
    }

    static bool TryIntersectLocalPoint(
        RectTransform zone,
        Camera cam,
        Vector2 screenPosition,
        out Vector2 localPoint)
    {
        localPoint = default;
        Ray ray = cam != null
            ? cam.ScreenPointToRay(screenPosition)
            : new Ray(new Vector3(screenPosition.x, screenPosition.y, 0f), Vector3.forward);

        Vector3 origin = zone.position;
        Vector3 normal = zone.rotation * Vector3.back;
        if (!new Plane(normal, origin).Raycast(ray, out float enter)
            && !new Plane(-normal, origin).Raycast(ray, out enter))
        {
            return false;
        }

        Vector3 local = zone.InverseTransformPoint(ray.GetPoint(enter));
        localPoint = new Vector2(local.x, local.y);
        return true;
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
