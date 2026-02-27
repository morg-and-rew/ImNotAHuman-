using System;
using UnityEngine;

public enum PlayerAttitude
{
    Neutral = 0,
    Mystical = 1,
    Skeptical = 2
}

[Serializable]
public sealed class PlayerAttitudeStats
{
    public int NeutralCount { get; private set; }
    public int MysticalCount { get; private set; }
    public int SkepticalCount { get; private set; }

    public event Action Changed;

    public void Add(PlayerAttitude attitude)
    {
        switch (attitude)
        {
            case PlayerAttitude.Mystical:
                MysticalCount++;
                break;

            case PlayerAttitude.Skeptical:
                SkepticalCount++;
                break;

            default:
                NeutralCount++;
                break;
        }

        Changed?.Invoke();
    }

    public void SetCounts(int neutral, int mystical, int skeptical)
    {
        NeutralCount = Mathf.Max(0, neutral);
        MysticalCount = Mathf.Max(0, mystical);
        SkepticalCount = Mathf.Max(0, skeptical);
        Changed?.Invoke();
    }
}
