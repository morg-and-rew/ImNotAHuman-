using UnityEngine;
using PixelCrushers.DialogueSystem;

public sealed class PhoneUnlockDirector : MonoBehaviour
{
    [Header("Dialogue System Vars")]
    [SerializeField] private string _flagVar = "unlock_phone";
    [SerializeField] private string _numberVar = "unlock_phone_number";

    [Header("Spawn")]
    [SerializeField] private PhoneNumberNote _notePrefab;
    [SerializeField] private Transform _spawnPoint;

    private PhoneNumberNote _spawned;

    public void TryUnlockFromDialogue()
    {
        int flag = DialogueLua.GetVariable(_flagVar).AsInt;
        if (flag != 1) return;

        string number = DialogueLua.GetVariable(_numberVar).AsString;
        if (string.IsNullOrWhiteSpace(number)) return;

        if (_notePrefab != null && _spawnPoint != null)
        {
            if (_spawned != null) Destroy(_spawned.gameObject);
            _spawned = Instantiate(_notePrefab, _spawnPoint.position, _spawnPoint.rotation);
            _spawned.SetNumber(number);
        }

        DialogueLua.SetVariable(_flagVar, 0);
    }
}
