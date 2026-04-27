using UnityEngine;

/// <summary>
/// Each <see cref="LateUpdate"/>, copies the local width and height of <see cref="sizeSource"/> onto this
/// <see cref="RectTransform"/> (<see cref="RectTransform.SetSizeWithCurrentAnchors"/>). Skips writes when the size is unchanged.
/// </summary>
[AddComponentMenu("UI/Rect Transform Copy Target Size")]
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class LayoutSelfSizeFromElement : MonoBehaviour
{
	[SerializeField] RectTransform sizeSource;

	RectTransform _rect;
	Vector2 _lastSourceSize;

	void OnEnable()
	{
		_rect = GetComponent<RectTransform>();
		_lastSourceSize = sizeSource ? sizeSource.rect.size : Vector2.zero;
		SyncNow();
	}

	void LateUpdate()
	{
		if (sizeSource == null)
			return;
		Vector2 s = sizeSource.rect.size;
		if (s == _lastSourceSize)
			return;
		_lastSourceSize = s;
		ApplySize(s);
	}

	/// <summary>Forces one copy immediately (e.g. before layout or when disabled).</summary>
	public void SyncNow()
	{
		if (sizeSource == null)
			return;
		Vector2 s = sizeSource.rect.size;
		_lastSourceSize = s;
		ApplySize(s);
	}

	void ApplySize(Vector2 size)
	{
		_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
		_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
	}
}
