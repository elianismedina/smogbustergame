using System.Collections;
using UnityEngine;

/// <summary>
/// Música de juego por capas (GDD 9.2): una base que suena siempre y una capa de tensión
/// (percusión y arpegio) que entra cuando el smog se acerca al 80%. Las dos pistas duran lo mismo
/// y arrancan en el mismo instante de DSP, así siguen en compás mientras hacen bucle.
/// Al terminar la partida se apagan con fundido para dejar sonar la victoria o la derrota.
/// Sale por el grupo Music del mixer, así respeta el volumen de música de Opciones.
/// </summary>
public class GameMusic : MonoBehaviour
{
    [SerializeField] private AudioClip _baseLayer;
    [SerializeField] private AudioClip _tensionLayer;
    [Range(0f, 1f)]
    [SerializeField] private float _baseVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _tensionVolume = 0.8f;

    [Header("Capa de tensión")]
    [Tooltip("Smog al que la capa empieza a sonar.")]
    [Range(0f, 1f)]
    [SerializeField] private float _tensionFrom = 0.7f;
    [Tooltip("Smog al que la capa suena entera.")]
    [Range(0f, 1f)]
    [SerializeField] private float _tensionFull = 0.8f;
    [Tooltip("Velocidad con la que la capa sigue al smog.")]
    [SerializeField] private float _smoothing = 2f;

    [Header("Fundidos")]
    [SerializeField] private float _fadeIn = 1.5f;
    [SerializeField] private float _fadeOutOnEnd = 1.2f;

    private GameSession _session;
    private AudioSource _base;
    private AudioSource _tension;
    private float _master;
    private float _tensionAmount;
    private bool _ending;

    private void Awake()
    {
        _base = CreateSource("Music Base", _baseLayer);
        _tension = CreateSource("Music Tension", _tensionLayer);
    }

    private void Start()
    {
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;

        if (AudioManager.Instance != null)
        {
            _base.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
            _tension.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
        }

        // Mismo instante de arranque: las capas quedan sincronizadas para siempre
        double start = AudioSettings.dspTime + 0.1;
        if (_baseLayer != null) _base.PlayScheduled(start);
        if (_tensionLayer != null) _tension.PlayScheduled(start);
    }

    private void OnDestroy()
    {
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    private void Update()
    {
        if (_ending) return;

        // Tiempo sin escalar: en pausa la música sigue (como en el AudioManager)
        float dt = Time.unscaledDeltaTime;
        _master = Mathf.MoveTowards(_master, 1f, dt / Mathf.Max(0.01f, _fadeIn));

        float target = _session != null ? Mathf.InverseLerp(_tensionFrom, _tensionFull, _session.Smog) : 0f;
        _tensionAmount = Mathf.Lerp(_tensionAmount, target, 1f - Mathf.Exp(-_smoothing * dt));

        _base.volume = _baseVolume * _master;
        _tension.volume = _tensionVolume * _master * _tensionAmount;
    }

    private void OnSessionEnded(GameSession.Result result)
    {
        if (_ending) return;
        _ending = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float baseStart = _base.volume;
        float tensionStart = _tension.volume;
        for (float t = 0f; t < _fadeOutOnEnd; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / _fadeOutOnEnd;
            _base.volume = baseStart * k;
            _tension.volume = tensionStart * k;
            yield return null;
        }
        _base.Stop();
        _tension.Stop();
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = true;
        return source;
    }
}
