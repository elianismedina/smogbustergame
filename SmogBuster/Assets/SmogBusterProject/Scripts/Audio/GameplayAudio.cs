using UnityEngine;

/// <summary>
/// Sonidos de juego (GDD 9.2) que no son del motor:
/// - Rayo Purificador: pulso en loop mientras se dispara, con fundido de entrada y salida.
/// - Nube disipada, disparo de semilla y florecimiento: efectos cortos.
/// - Alerta: pitido al pasar el smog del 80%, que se repite mientras siga por encima.
/// - Voces con su frase en pantalla: una al empezar a jugar (después del tutorial, si sale) y otra
///   cada vez que el smog baja al 20% ("el aire está casi limpio"), otra cuando se acaba el tiempo y otra al ganar.
/// Va en el dron, junto a <see cref="PurifierBeam"/> y <see cref="SeedLauncher"/>.
/// </summary>
public class GameplayAudio : MonoBehaviour
{
    [Header("Rayo Purificador")]
    [SerializeField] private AudioClip _beamLoop;
    [SerializeField] private float _beamVolume = 0.6f;
    [Tooltip("Segundos del fundido al empezar y dejar de disparar.")]
    [SerializeField] private float _beamFade = 0.08f;
    [SerializeField] private AudioClip _cloudPurified;
    [SerializeField] private float _cloudPurifiedVolume = 0.8f;

    [Header("Semillas")]
    [SerializeField] private AudioClip _seedLaunch;
    [SerializeField] private float _seedLaunchVolume = 0.8f;
    [SerializeField] private AudioClip _bloom;
    [SerializeField] private float _bloomVolume = 0.9f;

    [Header("Alerta de smog")]
    [SerializeField] private AudioClip _alert;
    [SerializeField] private float _alertVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _alertThreshold = 0.8f;
    [Tooltip("Segundos entre pitidos mientras el smog siga por encima del umbral.")]
    [SerializeField] private float _alertRepeat = 3f;

    [Header("Voz de inicio")]
    [SerializeField] private AudioClip _startVoice;
    [Tooltip("Frase que sale en el centro de la pantalla mientras suena la voz.")]
    [SerializeField] private string _startVoiceText = "Let's clean the air of this city!";
    [SerializeField] private float _startVoiceVolume = 1f;
    [Tooltip("Segundos de juego antes de la frase, para que el motor arranque primero. El tutorial no cuenta (pausa el juego).")]
    [SerializeField] private float _startVoiceDelay = 0.8f;

    [Header("Voz de aire casi limpio")]
    [SerializeField] private AudioClip _almostCleanVoice;
    [SerializeField] private string _almostCleanText = "The air is almost cleaned up!";
    [SerializeField] private float _almostCleanVolume = 1f;
    [Range(0f, 1f)]
    [Tooltip("Suena al bajar el smog hasta este nivel.")]
    [SerializeField] private float _almostCleanThreshold = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("Para volver a sonar, el smog tiene que subir antes por encima de este nivel (si no, sonaría sin parar al rozar el 20%).")]
    [SerializeField] private float _almostCleanRearm = 0.3f;

    [Header("Aviso de tiempo")]
    [SerializeField] private AudioClip _timeRunningOut;
    [SerializeField] private string _timeRunningOutText = "Time is running out!";
    [SerializeField] private float _timeRunningOutVolume = 1f;
    [Tooltip("Suena una vez al quedar estos segundos (el reloj del HUD se pone en aviso a los 30).")]
    [SerializeField] private float _timeRunningOutAt = 30f;

    [Header("Voz de victoria")]
    [SerializeField] private AudioClip _victoryVoice;
    [SerializeField] private string _victoryText = "Look at the blue sky!";
    [SerializeField] private float _victoryVolume = 1f;
    [Tooltip("Segundos tras ganar antes de la frase, para que arranque primero el sonido de victoria.")]
    [SerializeField] private float _victoryVoiceDelay = 0.4f;

    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private AudioSource _beamSource;
    private float _nextAlertTime;
    private bool _aboveThreshold;
    private float _playTime;

    /// <summary>Al sonar una voz: la frase y su duración en segundos (el HUD la muestra a la vez).</summary>
    public static event System.Action<string, float> VoiceLinePlayed;
    private bool _startVoicePlayed;
    private bool _almostCleanArmed = true;
    private bool _timeRunningOutPlayed;
    private float _victoryTime;
    private bool _victoryVoicePlayed;

    private void Awake()
    {
        _beam = GetComponent<PurifierBeam>();
        _seeds = GetComponent<SeedLauncher>();

        _beamSource = gameObject.AddComponent<AudioSource>();
        _beamSource.clip = _beamLoop;
        _beamSource.loop = true;
        _beamSource.playOnAwake = false;
        _beamSource.spatialBlend = 0f;
        _beamSource.volume = 0f;
    }

    private void Start()
    {
        if (AudioManager.Instance != null) _beamSource.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
    }

    private void OnEnable()
    {
        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnPlanted;
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
    }

    private void OnDisable()
    {
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnPlanted;
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
    }

    private void Update()
    {
        UpdateBeam();
        UpdateAlert();
        UpdateStartVoice();
        UpdateAlmostCleanVoice();
        UpdateTimeRunningOut();
        UpdateVictoryVoice();
    }

    // Cuenta tiempo escalado: con el tutorial abierto (timeScale 0) no avanza, así la frase suena al empezar a volar
    private void UpdateStartVoice()
    {
        if (_startVoicePlayed || _startVoice == null) return;
        GameSession session = GameSession.Instance;
        if (session != null && session.IsOver) return;

        _playTime += Time.deltaTime;
        if (_playTime < _startVoiceDelay) return;
        _startVoicePlayed = true;
        PlayVoice(_startVoice, _startVoiceText, _startVoiceVolume);
    }

    private void UpdateAlmostCleanVoice()
    {
        GameSession session = GameSession.Instance;
        if (_almostCleanVoice == null || session == null || session.IsOver) return;

        if (session.Smog > _almostCleanRearm) _almostCleanArmed = true;
        else if (_almostCleanArmed && session.Smog <= _almostCleanThreshold)
        {
            _almostCleanArmed = false;
            PlayVoice(_almostCleanVoice, _almostCleanText, _almostCleanVolume);
        }
    }

    private void UpdateTimeRunningOut()
    {
        if (_timeRunningOutPlayed || _timeRunningOut == null) return;
        GameSession session = GameSession.Instance;
        if (session == null || session.IsOver || session.TimeRemaining > _timeRunningOutAt) return;

        _timeRunningOutPlayed = true;
        PlayVoice(_timeRunningOut, _timeRunningOutText, _timeRunningOutVolume);
    }

    private void UpdateVictoryVoice()
    {
        GameSession session = GameSession.Instance;
        if (_victoryVoicePlayed || _victoryVoice == null || session == null || session.Outcome != GameSession.Result.Victory) return;

        _victoryTime += Time.deltaTime;
        if (_victoryTime < _victoryVoiceDelay) return;
        _victoryVoicePlayed = true;
        PlayVoice(_victoryVoice, _victoryText, _victoryVolume);
    }

    private static void PlayVoice(AudioClip clip, string text, float volume)
    {
        Play(clip, volume);
        VoiceLinePlayed?.Invoke(text, clip.length);
    }

    private void UpdateBeam()
    {
        if (_beamLoop == null) return;

        bool firing = _beam != null && _beam.IsFiring && Time.timeScale > 0f;
        float target = firing ? _beamVolume : 0f;
        float step = _beamVolume / Mathf.Max(0.01f, _beamFade) * Time.unscaledDeltaTime;
        _beamSource.volume = Mathf.MoveTowards(_beamSource.volume, target, step);

        if (firing && !_beamSource.isPlaying) _beamSource.Play();
        else if (!firing && _beamSource.isPlaying && _beamSource.volume <= 0f) _beamSource.Stop();
    }

    private void UpdateAlert()
    {
        GameSession session = GameSession.Instance;
        if (_alert == null || session == null || session.IsOver || Time.timeScale <= 0f)
        {
            _aboveThreshold = false;
            return;
        }

        bool above = session.Smog >= _alertThreshold;
        // Al cruzar el umbral suena enseguida; después, cada _alertRepeat segundos
        if (above && !_aboveThreshold) _nextAlertTime = Time.time;
        _aboveThreshold = above;

        if (above && Time.time >= _nextAlertTime)
        {
            Play(_alert, _alertVolume);
            _nextAlertTime = Time.time + _alertRepeat;
        }
    }

    // Variación leve de tono para que las repeticiones no suenen idénticas
    private void OnCloudPurified(SmogCloud cloud) => Play(_cloudPurified, _cloudPurifiedVolume, Random.Range(0.94f, 1.06f));

    private void OnSeedLaunched() => Play(_seedLaunch, _seedLaunchVolume, Random.Range(0.95f, 1.05f));

    private void OnPlanted(PlantingSpot spot) => Play(_bloom, _bloomVolume);

    private static void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip, volume, pitch);
    }
}
