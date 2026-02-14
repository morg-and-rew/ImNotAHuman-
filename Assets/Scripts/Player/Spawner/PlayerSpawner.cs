using UnityEngine;

[RequireComponent(typeof(PlayerSpawner))]
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private PlayerView _playerPrefab;
    [SerializeField] private Transform _spawnPoint;

    public PlayerView SpawnPlayer()
    {
        PlayerView playerInstance = Instantiate(_playerPrefab, _spawnPoint.position, _spawnPoint.rotation);
        return playerInstance;
    }
}
