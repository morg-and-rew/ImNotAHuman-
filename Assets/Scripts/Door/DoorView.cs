using UnityEngine;

/// <summary>
/// Подсказка у двери: при входе в триггер показывается канвас, при выходе — скрывается.
/// На объект двери нужен Collider с Is Trigger = true.
/// В Hint Canvas перетащи объект с Canvas (или дочерний UI), например текст «Нажми F» / «Выход к клиенту».
/// </summary>
public sealed class DoorView : MonoBehaviour
{
    [Header("Hint")]
    [SerializeField] private GameObject _hintCanvas;

    private void Start()
    {
        if (_hintCanvas != null)
            _hintCanvas.SetActive(false);
        LookAtCamera.Ensure(_hintCanvas);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _) && _hintCanvas != null)
            _hintCanvas.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerView>(out _) && _hintCanvas != null)
            _hintCanvas.SetActive(false);
    }
}
