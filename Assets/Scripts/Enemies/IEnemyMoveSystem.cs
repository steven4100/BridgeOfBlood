using BridgeOfBlood.Data.Enemies;

public interface IEnemyMoveSystem
{
    void MoveEnemies(EnemyBuffers enemies, float deltaTime);
}
