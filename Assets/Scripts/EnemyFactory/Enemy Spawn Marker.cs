using UnityEngine;

public class EnemySpawnMarker : MonoBehaviour
{
    [SerializeField] private EnemyType enemyType;
}

internal enum EnemyType
{
    Alarm, Bomb, Boxer, Crawler, Drone, ETurret, PTurret
}
