using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay host that reparents a caller-owned UI element, places it near an anchor, and keeps it on-screen.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(RectTransform))]
public class HoverTooltip : MonoBehaviour
{
	public static HoverTooltip Current { get; private set; }

	[SerializeField] float screenPadding = 8f;
	[SerializeField] float gap = 8f;

	readonly Vector3[] _corners = new Vector3[4];
	object _owner;
	RectTransform _content;
	Transform _homeParent;
	int _homeSiblingIndex;
	Vector2 _homeAnchorMin;
	Vector2 _homeAnchorMax;
	Vector2 _homePivot;
	Vector2 _homeAnchoredPosition;
	Vector2 _homeSizeDelta;
	Vector3 _homeLocalScale;
	Canvas _canvas;
	RectTransform _canvasRect;

	void Awake()
	{
		_canvas = GetComponent<Canvas>();
		_canvasRect = (RectTransform)transform;
	}

	void OnEnable()
	{
		if (Current == null)
			Current = this;
	}

	void OnDisable()
	{
		if (Current == this)
			Current = null;
		RestoreContent();
	}

	void LateUpdate()
	{
		if (_owner is Object unityOwner && unityOwner == null)
			RestoreContent();
	}

	public void Show(object owner, RectTransform content, RectTransform anchor)
	{
		if (_content != content)
		{
			RestoreContent();
			CaptureHome(content);
			content.SetParent(_canvasRect, false);
			content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
		}

		_owner = owner;
		content.gameObject.SetActive(true);
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		PlaceNear(content, anchor);
	}

	public void Hide(object owner)
	{
		if (!ReferenceEquals(_owner, owner))
			return;
		RestoreContent();
	}

	void CaptureHome(RectTransform content)
	{
		_content = content;
		_homeParent = content.parent;
		_homeSiblingIndex = content.GetSiblingIndex();
		_homeAnchorMin = content.anchorMin;
		_homeAnchorMax = content.anchorMax;
		_homePivot = content.pivot;
		_homeAnchoredPosition = content.anchoredPosition;
		_homeSizeDelta = content.sizeDelta;
		_homeLocalScale = content.localScale;
	}

	void RestoreContent()
	{
		if (_content == null)
		{
			_owner = null;
			return;
		}

		RectTransform content = _content;
		_content = null;
		_owner = null;

		if (content == null)
			return;

		content.SetParent(_homeParent, false);
		content.SetSiblingIndex(_homeSiblingIndex);
		content.anchorMin = _homeAnchorMin;
		content.anchorMax = _homeAnchorMax;
		content.pivot = _homePivot;
		content.anchoredPosition = _homeAnchoredPosition;
		content.sizeDelta = _homeSizeDelta;
		content.localScale = _homeLocalScale;
		content.gameObject.SetActive(false);
	}

	void PlaceNear(RectTransform content, RectTransform anchor)
	{
		anchor.GetWorldCorners(_corners);
		Camera worldCam = WorldCameraFor(anchor);
		Vector2 bl = RectTransformUtility.WorldToScreenPoint(worldCam, _corners[0]);
		Vector2 tl = RectTransformUtility.WorldToScreenPoint(worldCam, _corners[1]);
		Vector2 tr = RectTransformUtility.WorldToScreenPoint(worldCam, _corners[2]);
		Vector2 br = RectTransformUtility.WorldToScreenPoint(worldCam, _corners[3]);

		float minX = Mathf.Min(Mathf.Min(bl.x, tl.x), Mathf.Min(tr.x, br.x));
		float maxX = Mathf.Max(Mathf.Max(bl.x, tl.x), Mathf.Max(tr.x, br.x));
		float minY = Mathf.Min(Mathf.Min(bl.y, tl.y), Mathf.Min(tr.y, br.y));
		float maxY = Mathf.Max(Mathf.Max(bl.y, tl.y), Mathf.Max(tr.y, br.y));
		Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

		float scale = _canvas.scaleFactor;
		float w = content.rect.width * scale;
		float h = content.rect.height * scale;
		Vector2 pivot = content.pivot;

		Vector2 above = new Vector2(center.x + (pivot.x - 0.5f) * w, maxY + gap + pivot.y * h);
		Vector2 below = new Vector2(center.x + (pivot.x - 0.5f) * w, minY - gap - (1f - pivot.y) * h);
		Vector2 right = new Vector2(maxX + gap + pivot.x * w, center.y + (pivot.y - 0.5f) * h);
		Vector2 left = new Vector2(minX - gap - (1f - pivot.x) * w, center.y + (pivot.y - 0.5f) * h);

		SetPivotScreen(content, above);
		if (!FitsOnScreen(content))
		{
			SetPivotScreen(content, below);
			if (!FitsOnScreen(content))
			{
				SetPivotScreen(content, right);
				if (!FitsOnScreen(content))
					SetPivotScreen(content, left);
			}
		}

		ClampToScreen(content);
	}

	void SetPivotScreen(RectTransform content, Vector2 screenPoint)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out Vector2 local);
		content.anchoredPosition = local;
	}

	bool FitsOnScreen(RectTransform content)
	{
		GetScreenRect(content, out float minX, out float maxX, out float minY, out float maxY);
		return minX >= screenPadding
			&& maxX <= Screen.width - screenPadding
			&& minY >= screenPadding
			&& maxY <= Screen.height - screenPadding;
	}

	void ClampToScreen(RectTransform content)
	{
		GetScreenRect(content, out float minX, out float maxX, out float minY, out float maxY);
		float shiftX = Mathf.Max(0f, screenPadding - minX) - Mathf.Max(0f, maxX - (Screen.width - screenPadding));
		float shiftY = Mathf.Max(0f, screenPadding - minY) - Mathf.Max(0f, maxY - (Screen.height - screenPadding));
		if (shiftX == 0f && shiftY == 0f)
			return;
		content.anchoredPosition += new Vector2(shiftX, shiftY) / _canvas.scaleFactor;
	}

	void GetScreenRect(RectTransform content, out float minX, out float maxX, out float minY, out float maxY)
	{
		content.GetWorldCorners(_corners);
		Vector2 a = RectTransformUtility.WorldToScreenPoint(null, _corners[0]);
		Vector2 b = RectTransformUtility.WorldToScreenPoint(null, _corners[1]);
		Vector2 c = RectTransformUtility.WorldToScreenPoint(null, _corners[2]);
		Vector2 d = RectTransformUtility.WorldToScreenPoint(null, _corners[3]);
		minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
		maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
		minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
		maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
	}

	static Camera WorldCameraFor(RectTransform anchor)
	{
		Canvas source = anchor.GetComponentInParent<Canvas>();
		if (source == null || source.renderMode == RenderMode.ScreenSpaceOverlay)
			return null;
		return source.worldCamera;
	}
}
