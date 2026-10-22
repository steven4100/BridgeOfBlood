using UnityEngine;

public class SpellInputContext
{
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }

    public SpellInputContext(Vector2 origin, Vector2 direction)
    {
        Origin = origin;
        Direction = direction;
    }
}
