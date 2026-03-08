using UnityEngine;

[CreateAssetMenu(menuName = "BridgeOfBlood/Sprite Render Database")]
public class SpriteRenderDatabase : ScriptableObject
{
    public Texture2D atlas;
    public SpriteFrame[] frames;
}
