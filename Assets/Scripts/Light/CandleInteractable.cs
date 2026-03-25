using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CandleInteractable : MonoBehaviour, IWorldInteractable
{
    private static readonly List<CandleInteractable> AllCandles = new List<CandleInteractable>(32);

    [Header("Hint")]
    [SerializeField] private Sprite _hintSprite;

    [Header("Availability")]
    [SerializeField] private GameFlowController _flow;
    [SerializeField] private bool _availableOnlyFromDay2 = true;

    [Header("Behavior")]
    [SerializeField] private bool _igniteAllCandles = true;

    [Header("Visuals To Enable On Ignite")]
    [SerializeField] private Light[] _lightsToEnable = new Light[0];
    [SerializeField] private ParticleSystem[] _flamesToPlay = new ParticleSystem[0];
    [SerializeField] private GameObject[] _objectsToEnable = new GameObject[0];

    private bool _isLit;
    public static bool IsAnyCandleLit
    {
        get
        {
            for (int i = 0; i < AllCandles.Count; i++)
            {
                CandleInteractable candle = AllCandles[i];
                if (candle != null && candle._isLit)
                    return true;
            }

            return false;
        }
    }

    public Sprite HintSprite => CanInteractNow() ? _hintSprite : null;

    private void Awake()
    {
        if (!AllCandles.Contains(this))
            AllCandles.Add(this);
        if (_flow == null)
            _flow = GameFlowController.Instance;
    }

    private void OnDestroy()
    {
        AllCandles.Remove(this);
    }

    public void Interact(IPlayerInput input)
    {
        if (!CanInteractNow())
            return;

        if (_igniteAllCandles)
            IgniteAll();
        else
            IgniteSelf();
    }

    private bool CanInteractNow()
    {
        if (_isLit)
            return false;
        if (!_availableOnlyFromDay2)
            return true;
        return _flow != null && _flow.IsDay2OrLater();
    }

    private void IgniteAll()
    {
        for (int i = 0; i < AllCandles.Count; i++)
        {
            CandleInteractable candle = AllCandles[i];
            if (candle != null)
                candle.IgniteSelf();
        }
    }

    private void IgniteSelf()
    {
        if (_isLit)
            return;

        _isLit = true;

        for (int i = 0; i < _lightsToEnable.Length; i++)
        {
            if (_lightsToEnable[i] != null)
                _lightsToEnable[i].enabled = true;
        }

        for (int i = 0; i < _flamesToPlay.Length; i++)
        {
            if (_flamesToPlay[i] != null && !_flamesToPlay[i].isPlaying)
                _flamesToPlay[i].Play(true);
        }

        for (int i = 0; i < _objectsToEnable.Length; i++)
        {
            if (_objectsToEnable[i] != null)
                _objectsToEnable[i].SetActive(true);
        }
    }
}
