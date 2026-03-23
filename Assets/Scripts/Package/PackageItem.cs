using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class PackageItem : MonoBehaviour
{
    [SerializeField] private Text _numberText;
    [SerializeField] private PackageRegistry _registry;
    [Tooltip("Стабильный ID для сейва. Если пустой, вычисляется автоматически из пути в иерархии.")]
    [SerializeField] private string _saveId;
    private string _runtimeSaveId;

    public int Number { get; private set; }
    public string SaveId => string.IsNullOrEmpty(_saveId) ? _runtimeSaveId : _saveId;

    private void Awake()
    {
        _runtimeSaveId = BuildRuntimeSaveId();
        _registry.Register(this);
    }

    private void OnDestroy()
    {
        _registry.Unregister(this);
    }

    public void NotifyTakenFromWarehouse()
    {
        _registry?.NotifyPackageTaken(this);
    }

    public void SetNumber(int number)
    {
        Number = number;

        if (_numberText != null)
            _numberText.text = number.ToString();
    }

    private string BuildRuntimeSaveId()
    {
        Scene scene = gameObject.scene;
        string sceneName = scene.IsValid() ? scene.name : "UnknownScene";

        Transform t = transform;
        string path = t.name + "#" + t.GetSiblingIndex();
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "#" + t.GetSiblingIndex() + "/" + path;
        }
        return sceneName + ":" + path;
    }
}
