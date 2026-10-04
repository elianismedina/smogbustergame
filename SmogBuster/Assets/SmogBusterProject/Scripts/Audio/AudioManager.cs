using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Gestor de audio global. Se crea solo al arrancar el juego (desde Resources/AudioManager)
/// y persiste entre escenas.
/// - Música con fundido cruzado (dos AudioSource alternos).
/// - Ambiente en loop.
/// - Pool de AudioSource 2D para SFX y UI.
/// - Aplica los volúmenes de <see cref="GameSettings"/> al AudioMixer.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string ResourcePath = "AudioManager";
    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string EffectsParam = "EffectsVolume";
    private const float MinDb = -80f;

    [Header("Mixer")]
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private AudioMixerGroup _musicGroup;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private AudioMixerGroup _uiGroup;
    [SerializeField] private AudioMixerGroup _ambienceGroup;

    [Header("Ajustes")]
    [SerializeField] private float _defaultMusicFade = 1f;
    [SerializeField] private float _defaultAmbienceFade = 1.5f;
    [SerializeField] private int _sfxVoices = 10;

    private AudioSource[] _musicSources;
    private int _activeMusic;
    private AudioSource _ambienceSource;
    private AudioSource[] _sfxPool;
    private int _nextSfx;
    private Coroutine _musicFade;
    private float _musicVolume = 1f;
    private Coroutine _ambienceFade;

    public static AudioManager Instance { get; private set; }

    public AudioMixerGroup MusicGroup => _musicGroup;
    public AudioMixerGroup SfxGroup => _sfxGroup;
    public AudioMixerGroup UiGroup => _uiGroup;
    public AudioMixerGroup AmbienceGroup => _ambienceGroup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<AudioManager>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"AudioManager: no se encontró Resources/{ResourcePath}.prefab; el juego no tendrá sonido.");
            return;
        }

        Instantiate(prefab).name = "AudioManager";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicSources = new[] { CreateSource("Music A", _musicGroup, true), CreateSource("Music B", _musicGroup, true) };
        // La música sigue sonando con el juego en pausa (AudioListener.pause)
        foreach (AudioSource music in _musicSources) music.ignoreListenerPause = true;
        _ambienceSource = CreateSource("Ambience", _ambienceGroup, true);

        _sfxPool = new AudioSource[Mathf.Max(1, _sfxVoices)];
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            _sfxPool[i] = CreateSource($"SFX {i}", _sfxGroup, false);
        }
    }

    private void Start()
    {
        // AudioMixer.SetFloat no funciona en Awake: se aplica aquí
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #region Volumen

    /// <summary>Lee <see cref="GameSettings"/> y actualiza el mixer. Llamar tras mover un slider.</summary>
    public void ApplyVolumes()
    {
        if (_mixer == null) return;
        _mixer.SetFloat(MusicParam, LinearToDecibels(GameSettings.MusicVolume));
        _mixer.SetFloat(EffectsParam, LinearToDecibels(GameSettings.SfxVolume));
    }

    /// <summary>Volumen general (p. ej. para silenciar al pausar). 0-1.</summary>
    public void SetMasterVolume(float linear)
    {
        if (_mixer != null) _mixer.SetFloat(MasterParam, LinearToDecibels(linear));
    }

    // Escala logarítmica: el slider se percibe lineal
    private static float LinearToDecibels(float linear)
    {
        return linear <= 0.0001f ? MinDb : Mathf.Max(MinDb, 20f * Mathf.Log10(linear));
    }

    #endregion

    #region Música y ambiente

    /// <param name="volume">Volumen propio de la pista (0-1), aparte del slider de música.</param>
    public void PlayMusic(AudioClip clip, float fadeDuration = -1f, float volume = 1f)
    {
        if (fadeDuration < 0f) fadeDuration = _defaultMusicFade;
        volume = Mathf.Clamp01(volume);

        AudioSource current = _musicSources[_activeMusic];
        if (clip != null && current.clip == clip && current.isPlaying)
        {
            // Misma pista: solo se ajusta el volumen (el fundido en curso lo recoge)
            _musicVolume = volume;
            if (_musicFade == null) current.volume = volume;
            return;
        }

        _activeMusic = 1 - _activeMusic;
        AudioSource next = _musicSources[_activeMusic];
        next.clip = clip;
        next.volume = 0f;
        _musicVolume = volume;
        if (clip != null) next.Play();

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(Crossfade(current, next, fadeDuration));
    }

    public void StopMusic(float fadeDuration = -1f) => PlayMusic(null, fadeDuration);

    public void PlayAmbience(AudioClip clip, float fadeDuration = -1f)
    {
        if (fadeDuration < 0f) fadeDuration = _defaultAmbienceFade;
        if (clip != null && _ambienceSource.clip == clip && _ambienceSource.isPlaying) return;

        if (_ambienceFade != null) StopCoroutine(_ambienceFade);
        _ambienceFade = StartCoroutine(SwapAmbience(clip, fadeDuration));
    }

    public void StopAmbience(float fadeDuration = -1f) => PlayAmbience(null, fadeDuration);

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
    {
        float startFrom = from.volume;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            if (to.clip != null) to.volume = Mathf.Lerp(0f, _musicVolume, k);
            yield return null;
        }

        from.volume = 0f;
        from.Stop();
        from.clip = null;
        if (to.clip != null) to.volume = _musicVolume;
        _musicFade = null;
    }

    private IEnumerator SwapAmbience(AudioClip clip, float duration)
    {
        float half = duration / 2f;
        if (_ambienceSource.isPlaying)
        {
            float start = _ambienceSource.volume;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                _ambienceSource.volume = Mathf.Lerp(start, 0f, t / half);
                yield return null;
            }
            _ambienceSource.Stop();
        }

        _ambienceSource.clip = clip;
        if (clip != null)
        {
            _ambienceSource.volume = 0f;
            _ambienceSource.Play();
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                _ambienceSource.volume = Mathf.Lerp(0f, 1f, t / half);
                yield return null;
            }
            _ambienceSource.volume = 1f;
        }
        _ambienceFade = null;
    }

    #endregion

    #region SFX

    /// <summary>Efecto 2D (no posicional) por el grupo SFX.</summary>
    public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f) => PlayOneShot(clip, _sfxGroup, volume, pitch);

    /// <summary>Sonido de interfaz por el grupo UI.</summary>
    public void PlayUi(AudioClip clip, float volume = 1f) => PlayOneShot(clip, _uiGroup, volume, 1f);

    private void PlayOneShot(AudioClip clip, AudioMixerGroup group, float volume, float pitch)
    {
        if (clip == null) return;

        AudioSource source = NextFreeSource();
        source.outputAudioMixerGroup = group;
        source.ignoreListenerPause = group == _uiGroup; // los clics del menú de pausa deben oírse
        source.pitch = pitch;
        source.volume = volume;
        source.clip = clip;
        source.Play();
    }

    private AudioSource NextFreeSource()
    {
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            AudioSource candidate = _sfxPool[(_nextSfx + i) % _sfxPool.Length];
            if (!candidate.isPlaying)
            {
                _nextSfx = (_nextSfx + i + 1) % _sfxPool.Length;
                return candidate;
            }
        }

        // Todas ocupadas: se reutiliza la más antigua
        AudioSource oldest = _sfxPool[_nextSfx];
        _nextSfx = (_nextSfx + 1) % _sfxPool.Length;
        return oldest;
    }

    #endregion

    private AudioSource CreateSource(string sourceName, AudioMixerGroup group, bool loop)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = group;
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }
}
