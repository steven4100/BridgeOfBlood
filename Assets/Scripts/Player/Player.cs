using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Minimal player: a position that moves within a rect via WASD input.
/// Plain class — call Update() each frame from the game loop.
/// </summary>
public class Player
{
    public float2 Position;
    public float MoveSpeed;

    public Player(float2 startPosition, float moveSpeed)
    {
        Position = startPosition;
        MoveSpeed = moveSpeed;
    }

    /// <summary>
    /// Reads WASD / arrow key input, moves the player, and clamps to the given bounds.
    /// </summary>
    public void Update(float deltaTime, Rect bounds)
    {
        float2 input = ReadMovementInput();
        if (math.lengthsq(input) > 1f)
            input = math.normalize(input);

        Position += input * MoveSpeed * deltaTime;

        Position.x = math.clamp(Position.x, bounds.xMin, bounds.xMax);
        Position.y = math.clamp(Position.y, bounds.yMin, bounds.yMax);
    }

    static float2 ReadMovementInput()
    {
        float2 dir = float2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    dir.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  dir.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  dir.x -= 1f;
        return dir;
    }
}
