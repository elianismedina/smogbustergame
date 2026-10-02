using UnityEngine;

/// <summary>
/// Define la música y el ambiente de una escena. Al empezar la escena se los pasa al AudioManager
/// (con fundido). Si un clip está vacío, ese canal se detiene.
/// </summary>
public class SceneAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _music;
    [SerializeField] private AudioClip _ambience;
    [Tooltip("Duración del fundido; -1 usa el valor por defecto del AudioManager.")]
    [SerializeField] private float _fadeDuration = -1f;

    private void Start()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;

        audio.PlayMusic(_music, _fadeDuration);
        audio.PlayAmbience(_ambience, _fadeDuration);
    }
}
