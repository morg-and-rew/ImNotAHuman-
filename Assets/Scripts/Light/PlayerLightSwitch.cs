using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerLightSwitch
{
    private IPlayerInput _input;

    public void Initialize(IPlayerInput input)
    {
        _input = input;
    }

    public void Tick()
    {
        if (_input != null && _input.InteractPressed)
        {
            ToggleAllLights();
        }
    }

    private void ToggleAllLights()
    {
        List<LightSwitch> lightSwitchesInZone = LightSwitchManager.Instance?.GetLightSwitchesInZone();

        if (lightSwitchesInZone != null)
        {
            foreach (LightSwitch lightSwitch in lightSwitchesInZone)
            {
                LightSwitchManager.Instance?.TurnOnLights(lightSwitch);
            }
        }
    }
}