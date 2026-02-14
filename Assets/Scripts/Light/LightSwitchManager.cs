using System.Collections.Generic;
using UnityEngine;

public sealed class LightSwitchManager : MonoBehaviour
{
    private static LightSwitchManager _instance;
    public static LightSwitchManager Instance => _instance;

    private List<LightSwitch> _lightSwitches = new List<LightSwitch>();
    private Dictionary<LightSwitch, Light[]> _lightSwitchToLights = new Dictionary<LightSwitch, Light[]>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterLightSwitch(LightSwitch lightSwitch, Light[] lights)
    {
        if (!_lightSwitches.Contains(lightSwitch))
        {
            _lightSwitches.Add(lightSwitch);

            if (lights != null)
            {
                _lightSwitchToLights[lightSwitch] = lights;

                foreach (Light light in lights)
                {
                    if (light != null)
                    {
                        light.enabled = false;
                    }
                }
            }
        }
    }

    public void UnregisterLightSwitch(LightSwitch lightSwitch)
    {
        _lightSwitches.Remove(lightSwitch);
        _lightSwitchToLights.Remove(lightSwitch);
    }

    public List<LightSwitch> GetLightSwitchesInZone()
    {
        List<LightSwitch> switchesInZone = new List<LightSwitch>();

        foreach (LightSwitch lightSwitch in _lightSwitches)
        {
            if (lightSwitch.IsPlayerInZone)
            {
                switchesInZone.Add(lightSwitch);
            }
        }

        return switchesInZone;
    }

    public void TurnOnLights(LightSwitch lightSwitch)
    {
        if (_lightSwitchToLights.ContainsKey(lightSwitch))
        {
            Light[] lights = _lightSwitchToLights[lightSwitch];
            foreach (Light light in lights)
            {
                if (light != null)
                {
                    light.enabled = true;
                }
            }
        }
    }
}