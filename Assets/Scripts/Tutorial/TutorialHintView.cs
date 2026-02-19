using UnityEngine;
using TMPro;

public sealed class TutorialHintView : MonoBehaviour
{
    [Tooltip("Только панель подсказки обучения. Не назначать весь PlayerCanvas — иначе при скрытии подсказки скроются и портреты клиента.")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _text;

    public static TutorialHintView Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _root.SetActive(false);
    }

    public void Show(string text)
    {
        _text.text = text;
        EnsureParentChainActive();
        _root.SetActive(true);
    }

    private void EnsureParentChainActive()
    {
        if (_root == null) return;
        Transform t = _root.transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    public void Hide()
    {
        if (_root == null) return;
        _root.SetActive(false);
    }
}

public enum TutorialStep
{
    None,
    PressSpace,
    GoToRouter,
    GoToPhone
}