using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Shared GraphicsBuffer bridge for VFX graphs that consume simulation-local enemy positions.
/// Subclasses fill <see cref="_positions"/> and then upload the active prefix.
/// </summary>
public abstract class EnemyPositionVfxController : MonoBehaviour
{
    const int PositionStrideBytes = sizeof(float) * 3;

    [SerializeField] protected VisualEffect effect;
    [SerializeField] int initialCapacity = 256;

    protected Vector3[] _positions;

    int _positionsPropertyId;
    int _countPropertyId;
    GraphicsBuffer _positionsBuffer;
    int _bufferCapacity;

    protected virtual void Awake()
    {
        _positionsPropertyId = Shader.PropertyToID("positions");
        _countPropertyId = Shader.PropertyToID("count");

        EnsureCpuCapacity(initialCapacity);
        EnsureBufferCapacity(Mathf.Max(1, initialCapacity));
        UploadPositionCount(0);
    }

    protected virtual void OnDisable()
    {
        ReleaseBuffer();
    }

    protected virtual void OnDestroy()
    {
        ReleaseBuffer();
    }

    protected void UploadPositions(int count)
    {
        EnsureCpuCapacity(count);
        EnsureBufferCapacity(Mathf.Max(1, count));

        if (count > 0)
            _positionsBuffer.SetData(_positions, 0, 0, count);

        UploadPositionCount(count);
    }

    protected void UploadAndPlay(int count)
    {
        UploadPositions(count);
        effect.Play();
    }

    protected void EnsureCpuCapacity(int requiredCapacity)
    {
        int capacity = Mathf.Max(1, requiredCapacity);
        if (_positions != null && _positions.Length >= capacity)
            return;

        int newCapacity = _positions != null ? _positions.Length : Mathf.Max(1, initialCapacity);
        while (newCapacity < capacity)
            newCapacity *= 2;

        System.Array.Resize(ref _positions, newCapacity);
    }

    void EnsureBufferCapacity(int requiredCapacity)
    {
        int capacity = Mathf.Max(1, requiredCapacity);
        if (_positionsBuffer != null && _bufferCapacity >= capacity)
            return;

        ReleaseBuffer();

        _bufferCapacity = capacity;
        _positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _bufferCapacity, PositionStrideBytes);
        effect.SetGraphicsBuffer(_positionsPropertyId, _positionsBuffer);
    }

    void UploadPositionCount(int count)
    {
        effect.SetGraphicsBuffer(_positionsPropertyId, _positionsBuffer);
        effect.SetInt(_countPropertyId, count);
    }

    void ReleaseBuffer()
    {
        if (_positionsBuffer == null)
            return;

        _positionsBuffer.Release();
        _positionsBuffer = null;
        _bufferCapacity = 0;
    }
}
