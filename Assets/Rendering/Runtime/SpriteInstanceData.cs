using System.Runtime.InteropServices;
using Unity.Mathematics;

/// <summary>
/// GPU instance row for <c>BridgeOfBlood/InstancedSprite</c>. Explicit layout so C# upload matches HLSL
/// <c>StructuredBuffer</c> stride (48 bytes); avoids runtime padding differences vs <c>Sequential</c>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 48)]
public struct SpriteInstanceData
{
    [FieldOffset(0)] public float4 positionScale;
    [FieldOffset(16)] public float4 uvRect;
    /// <summary>Additive RGB overlay for status VFX (default 0; alpha unused).</summary>
    [FieldOffset(32)] public float4 color;
}
