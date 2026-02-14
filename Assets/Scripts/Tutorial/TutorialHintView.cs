using UnityEngine;
using TMPro;

public sealed class TutorialHintView : MonoBehaviour
{
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
        _root.SetActive(true);
    }

    public void Hide()
    {
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