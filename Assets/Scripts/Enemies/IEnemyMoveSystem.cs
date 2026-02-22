using BridgeOfBlood.Data.Enemies;
using Unity.Collections;

public interface IEnemyMoveSystem
{
    void MoveEnemies(NativeArray<Enemy> enemies, float deltaTime);
}
