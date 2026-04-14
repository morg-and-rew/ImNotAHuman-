using System;
using UnityEngine;

/// <summary>
/// Громкость музыки и эффектов из главного меню (PlayerPrefs). Читается при старте и после переключений.
/// </summary>
public static class GameAudioSettings
{
    private const string MusicKey = "GameAudio_MusicVolume01";
    private const string SfxKey = "GameAudio_SfxVolume01";

    private static bool _loaded;

    public static float MusicVolume01 { get; private set; } = 1f;
    public static float SfxVolume01 { get; private set; } = 1f;

    public static event Action Changed;

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;
        MusicVolume01 = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        SfxVolume01 = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
        _loaded = true;
    }

    public static float ScaleSfx(float baseVolume)
    {
        EnsureLoaded();
        return Mathf.Clamp01(baseVolume * SfxVolume01);
    }

    public static void SetMusicVolume01(float value)
    {
        EnsureLoaded();
        MusicVolume01 = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume01);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetSfxVolume01(float value)
    {
        EnsureLoaded();
        SfxVolume01 = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume01);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ToggleMusicMuted()
    {
        SetMusicVolume01(MusicVolume01 < 0.5f ? 1f : 0f);
    }

    public static void ToggleSfxMuted()
    {
        SetSfxVolume01(SfxVolume01 < 0.5f ? 1f : 0f);
    }
}
