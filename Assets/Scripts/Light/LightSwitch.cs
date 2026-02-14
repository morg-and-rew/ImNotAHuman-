using System.Collections.Generic;
using UnityEngine;

public sealed class LightSwitch : MonoBehaviour
{
    [SerializeField] private Light[] _lights; // Массив источников света для переключения

    private bool _isPlayerInZone = false;

    public bool IsPlayerInZone => _isPlayerInZone;

    private void Start()
    {
        LightSwitchManager.Instance?.RegisterLightSwitch(this, _lights);
    }

    private void OnDestroy()
    {
        LightSwitchManager.Instance?.UnregisterLightSwitch(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
        {
            _isPlayerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerView playerView))
        {
            _isPlayerInZone = false;
        }
    }
}