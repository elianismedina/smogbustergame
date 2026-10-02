using UnityEngine;

/// <summary>
/// Opciones del jugador guardadas en PlayerPrefs (volúmenes, vibración).
/// Punto único de lectura/escritura para el menú, el AudioManager y el gameplay.
/// </summary>
public static class GameSettings
{
    public const string PrefMusicVolume = "options.musicVolume";
    public const string PrefSfxVolume = "options.sfxVolume";
    public const string PrefVibration = "options.vibration";

    private const float DefaultVolume = 0.8f;

    /// <summary>Volumen de música, 0-1.</summary>
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(PrefMusicVolume, DefaultVolume);
        set => PlayerPrefs.SetFloat(PrefMusicVolume, Mathf.Clamp01(value));
    }

    /// <summary>Volumen de efectos (SFX, UI y ambiente), 0-1.</summary>
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(PrefSfxVolume, DefaultVolume);
        set => PlayerPrefs.SetFloat(PrefSfxVolume, Mathf.Clamp01(value));
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(PrefVibration, 1) == 1;
        set => PlayerPrefs.SetInt(PrefVibration, value ? 1 : 0);
    }

    public static void Save() => PlayerPrefs.Save();
}
