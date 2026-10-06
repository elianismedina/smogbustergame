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
    public const string PrefFixedJoystick = "options.fixedJoystick";
    public const string PrefDifficulty = "options.difficulty";
    public const string PrefFirstPersonView = "options.firstPersonView";

    /// <summary>Dificultad ajustable (GDD 10.2 y 10.5): velocidad del smog y tiempo extra.</summary>
    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard,
    }

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

    /// <summary>Joystick fijo en su sitio (true) o dinámico, que aparece bajo el pulgar (false, por defecto). GDD 10.4.</summary>
    public static bool FixedJoystick
    {
        get => PlayerPrefs.GetInt(PrefFixedJoystick, 0) == 1;
        set => PlayerPrefs.SetInt(PrefFixedJoystick, value ? 1 : 0);
    }

    /// <summary>Última vista elegida con el botón de cámara del HUD (false = tercera persona).</summary>
    public static bool FirstPersonView
    {
        get => PlayerPrefs.GetInt(PrefFirstPersonView, 0) == 1;
        set => PlayerPrefs.SetInt(PrefFirstPersonView, value ? 1 : 0);
    }

    /// <summary>True si el jugador ya eligió una vista alguna vez (si no, manda la del Inspector).</summary>
    public static bool HasViewChoice => PlayerPrefs.HasKey(PrefFirstPersonView);

    public static DifficultyLevel Difficulty
    {
        get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefDifficulty, (int)DifficultyLevel.Normal), 0, 2);
        set => PlayerPrefs.SetInt(PrefDifficulty, (int)value);
    }

    /// <summary>Nombre para mostrar en Opciones.</summary>
    public static string DifficultyName(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return "FÁCIL";
            case DifficultyLevel.Hard: return "DIFÍCIL";
            default: return "NORMAL";
        }
    }

    /// <summary>Multiplicador de los segundos entre subidas de smog (más alto = sube más despacio).</summary>
    public static float SmogIntervalMultiplier(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return 1.8f;
            case DifficultyLevel.Hard: return 0.9f;
            default: return 1f;
        }
    }

    /// <summary>Segundos que se suman (o restan) al tiempo del nivel.</summary>
    public static float ExtraTime(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy: return 30f;
            case DifficultyLevel.Hard: return -30f;
            default: return 0f;
        }
    }

    public static void Save() => PlayerPrefs.Save();
}
