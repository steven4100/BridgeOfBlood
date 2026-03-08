using UnityEngine;

public abstract class SpriteProvider : ScriptableObject
{
    public abstract EntityVisual Resolve(uint seed);
}
