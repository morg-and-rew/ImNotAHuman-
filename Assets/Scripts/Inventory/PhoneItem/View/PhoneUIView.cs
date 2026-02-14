using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PhoneUIView : MonoBehaviour
{
    public event Action<char> DigitPressed;
    public event Action BackspacePressed;
    public event Action CallPressed;
    public event Action ClosePressed;

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _numberText;

    [SerializeField] private Canvas _canvas;

    [Header("Buttons")]
    [SerializeField] private Button _callButton;
    [SerializeField] private Button _backspaceButton;
    [SerializeField] private Button _closeButton;

    [Header("Digit Buttons Root (optional)")]
    [SerializeField] private Transform _digitsRoot;

    [SerializeField] private float _errorShowSeconds = 1.2f;

    private Coroutine _errorRoutine;
    private readonly Dictionary<Button, char> _buttonToDigit = new();

    private void Awake()
    {
        HideImmediate();

        if (_callButton != null) _callButton.onClick.AddListener(() => CallPressed?.Invoke());
        if (_backspaceButton != null) _backspaceButton.onClick.AddListener(() => BackspacePressed?.Invoke());
        if (_closeButton != null) _closeButton.onClick.AddListener(() => ClosePressed?.Invoke());

        BuildDigitDictionary();
    }

    private void BuildDigitDictionary()
    {
        _buttonToDigit.Clear();

        Transform root = _digitsRoot != null ? _digitsRoot : transform;

        PhoneDigitButton[] digitButtons = root.GetComponentsInChildren<PhoneDigitButton>(true);
        for (int i = 0; i < digitButtons.Length; i++)
        {
            PhoneDigitButton db = digitButtons[i];
            if (db == null || db.Button == null) continue;

            _buttonToDigit[db.Button] = db.Digit;

            char d = db.Digit;
            db.Button.onClick.AddListener(() => DigitPressed?.Invoke(d));
        }

        if (digitButtons.Length == 0)
            Debug.LogWarning("PhoneUIView: no PhoneDigitButton found. Digit buttons will not work.");
    }

    public void Show()
    {
        gameObject.SetActive(true);

        if (_group != null)
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
    }

    public void Hide() => HideImmediate();

    public void SetNumber(string number)
    {
        if (_numberText != null)
            _numberText.text = number ?? "";
    }

    public void SetCallInteractable(bool value)
    {
        if (_callButton != null)
            _callButton.interactable = value;
    }

    private void HideImmediate()
    {
        if (_errorRoutine != null)
        {
            StopCoroutine(_errorRoutine);
            _errorRoutine = null;
        }

        if (_group != null)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    public void SetEventCamera(Camera cam)
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvas == null || cam == null)
            return;

        _canvas.worldCamera = cam;
    }

    public void ShowInvalidNumber()
    {
        ShowTempMessage("Íîìåð íàáðàí íåïðàâèëüíî", _errorShowSeconds);
    }

    private void ShowTempMessage(string message, float seconds)
    {
        if (_errorRoutine != null)
        {
            StopCoroutine(_errorRoutine);
            _errorRoutine = null;
        }

        _errorRoutine = StartCoroutine(ShowTempMessageRoutine(message, seconds));
    }

    private IEnumerator ShowTempMessageRoutine(string message, float seconds)
    {
        string prev = _numberText != null ? _numberText.text : "";

        if (_numberText != null)
            _numberText.text = message;

        yield return WaitForSecondsCache.Get(seconds);

        if (_numberText != null)
            _numberText.text = prev;

        _errorRoutine = null;
    }
}
