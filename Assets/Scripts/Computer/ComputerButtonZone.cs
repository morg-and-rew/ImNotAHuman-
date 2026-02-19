using UnityEngine;

/// <summary>
/// Вешается на объект кнопки (улица или помещение) с коллайдером-триггером.
/// При входе/выходе игрока сообщает Computer, в какой зоне кнопки тот находится.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class ComputerButtonZone : MonoBehaviour
{
    [SerializeField] private string _kind = Computer.KindIndoor;
    [SerializeField] private Computer _computer;

    private void Awake()
    {
        if (_computer == null)
            _computer = GetComponentInParent<Computer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _) && _computer != null)
            _computer.SetPlayerInButtonZone(_kind, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _) && _computer != null)
            _computer.SetPlayerInButtonZone(_kind, false);
    }
}
