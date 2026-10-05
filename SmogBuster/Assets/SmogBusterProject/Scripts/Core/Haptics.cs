using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vibración suave (GDD 10.3). Respeta la opción del menú (<see cref="GameSettings.Vibration"/>).
/// - Android: pulso corto con duración e intensidad (Vibrator / VibrationEffect).
/// - iOS: Handheld.Vibrate solo en pulsos fuertes, porque no admite duración y es intensa.
/// - Mando: rumble breve en el gamepad conectado.
/// En el Editor solo vibra el mando.
/// </summary>
public static class Haptics
{
    public enum Strength
    {
        Light,
        Medium,
        Heavy,
    }

    private static Runner _runner;
    private static Coroutine _rumble;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static int _sdk;
#endif

    public static void Pulse(Strength strength)
    {
        if (!GameSettings.Vibration) return;

        Durations(strength, out long millis, out int amplitude, out float motor);
        Rumble(motor, millis / 1000f);

#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(millis, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
        if (strength == Strength.Heavy) Handheld.Vibrate();
#endif
    }

    private static void Durations(Strength strength, out long millis, out int amplitude, out float motor)
    {
        switch (strength)
        {
            case Strength.Heavy: millis = 180; amplitude = 255; motor = 0.8f; break;
            case Strength.Medium: millis = 60; amplitude = 160; motor = 0.45f; break;
            default: millis = 25; amplitude = 80; motor = 0.2f; break;
        }
    }

    private static void Rumble(float motor, float seconds)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        if (_runner == null)
        {
            var go = new GameObject("Haptics");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _runner = go.AddComponent<Runner>();
        }
        if (_rumble != null) _runner.StopCoroutine(_rumble);
        _rumble = _runner.StartCoroutine(RumbleFor(pad, motor, seconds));
    }

    private static IEnumerator RumbleFor(Gamepad pad, float motor, float seconds)
    {
        pad.SetMotorSpeeds(motor * 0.6f, motor);
        yield return new WaitForSecondsRealtime(seconds);
        pad.SetMotorSpeeds(0f, 0f);
        _rumble = null;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void VibrateAndroid(long millis, int amplitude)
    {
        try
        {
            if (_vibrator == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdk = version.GetStatic<int>("SDK_INT");
                }
            }

            if (_sdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", millis, amplitude))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                _vibrator.Call("vibrate", millis);
            }
        }
        catch (System.Exception)
        {
            // Sin acceso al Vibrator: vibración estándar (también hace que Unity pida el permiso VIBRATE)
            Handheld.Vibrate();
        }
    }
#endif

    private class Runner : MonoBehaviour
    {
        // El rumble no debe quedarse encendido si se sale de la escena o se cierra el juego
        private void OnDisable() => Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }
}
