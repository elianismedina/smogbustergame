using UnityEngine;

/// <summary>
/// Define la música y el ambiente de una escena. Al empezar la escena se los pasa al AudioManager
/// (con fundido). Si un clip está vacío, ese canal se detiene.
/// </summary>
public class SceneAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _music;
    [Tooltip("Volumen de la música de esta escena (0-1). Se multiplica por el slider de música de Opciones.")]
    [Range(0f, 1f)]
    [SerializeField] private float _musicVolume = 1f;
    [SerializeField] private AudioClip _ambience;
    [Tooltip("Duración del fundido; -1 usa el valor por defecto del AudioManager.")]
    [SerializeField] private float _fadeDuration = -1f;

    private void Start()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;

        audio.PlayMusic(_music, _fadeDuration, _musicVolume);
        audio.PlayAmbience(_ambience, _fadeDuration);
    }
}
