using TMPro;
using UnityEngine;

public sealed class DeliveryNoteView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _text;

    private void Awake()
    {
        Hide();
    }

    public void ShowNumber(int number)
    {
        if (_text != null) _text.text = number.ToString();
        if (_root != null) _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
        if (_text != null) _text.text = "";
    }
}
