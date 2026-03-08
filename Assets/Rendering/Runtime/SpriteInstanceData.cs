using System.Runtime.InteropServices;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential)]
public struct SpriteInstanceData
{
    public float3 position;
    public float scale;
    public float4 uvRect;
}
