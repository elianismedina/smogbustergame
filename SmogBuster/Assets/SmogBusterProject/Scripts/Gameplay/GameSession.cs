using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reglas de la partida (GDD 5.2 y 5.3): nivel global de smog, temporizador y fin de partida.
/// - El smog empieza en 60% y sube 1% cada 2 s.
/// - Victoria: smog al 0% antes de que acabe el tiempo.
/// - Derrota: smog al 100%, tiempo a 00:00 o choque contra un edificio.
/// Usa tiempo escalado, así que se detiene con la pausa (timeScale = 0).
/// </summary>
public class GameSession : MonoBehaviour
{
    public enum Result
    {
        None,
        Victory,
        Saturation,
        TimeUp,
        Crash,
    }

    [Header("Smog (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _startSmog = 0.6f;
    [Tooltip("Cuánto sube el smog en cada paso.")]
    [SerializeField] private float _riseAmount = 0.01f;
    [Tooltip("Segundos entre cada subida.")]
    [SerializeField] private float _riseInterval = 2f;

    [Header("Tiempo")]
    [Tooltip("Duración del nivel en segundos.")]
    [SerializeField] private float _timeLimit = 180f;

    [Header("Referencias (se buscan en la escena si están vacías)")]
    [SerializeField] private QuadcopterCrash _player;
    [Tooltip("Nubes de ambiente: su densidad sigue al nivel de smog.")]
    [SerializeField] private SmogClouds _clouds;

    [Header("Depuración")]
    [Tooltip("En el Editor y en builds de desarrollo, K baja el smog y L lo sube.")]
    [SerializeField] private float _debugStep = 0.1f;

    private QuadInput _input;
    private float _riseTimer;

    public static GameSession Instance { get; private set; }

    /// <summary>Nivel global de smog (0-1).</summary>
    public float Smog { get; private set; }

    /// <summary>Segundos que quedan.</summary>
    public float TimeRemaining { get; private set; }

    public float TimeLimit => _timeLimit;
    public Result Outcome { get; private set; } = Result.None;
    public bool IsOver => Outcome != Result.None;

    /// <summary>Se lanza una sola vez, al terminar la partida.</summary>
    public event Action<Result> Ended;

    private void Awake()
    {
        Instance = this;
        Smog = Mathf.Clamp01(_startSmog);
        TimeRemaining = _timeLimit;
    }

    private void OnEnable()
    {
        if (_player == null) _player = FindAnyObjectByType<QuadcopterCrash>();
        if (_clouds == null) _clouds = FindAnyObjectByType<SmogClouds>();
        if (_player != null)
        {
            _input = _player.GetComponent<QuadInput>();
            _player.Crashed += OnPlayerCrashed;
        }

        if (_clouds != null) _clouds.Density = Smog;
    }

    private void OnDisable()
    {
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOver) return;

        HandleDebugKeys();

        _riseTimer += Time.deltaTime;
        while (_riseTimer >= _riseInterval)
        {
            _riseTimer -= _riseInterval;
            SetSmog(Smog + _riseAmount);
        }

        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);

        if (Smog <= 0f) End(Result.Victory);
        else if (Smog >= 1f) End(Result.Saturation);
        else if (TimeRemaining <= 0f) End(Result.TimeUp);
    }

    /// <summary>Baja el smog (p. ej. 0.05 = 5% al disipar una nube).</summary>
    public void ReduceSmog(float amount)
    {
        if (IsOver || amount <= 0f) return;
        SetSmog(Smog - amount);
    }

    /// <summary>Sube el smog (p. ej. una micro-nube que no se neutralizó).</summary>
    public void AddSmog(float amount)
    {
        if (IsOver || amount <= 0f) return;
        SetSmog(Smog + amount);
    }

    private void SetSmog(float value)
    {
        Smog = Mathf.Clamp01(value);
        if (_clouds != null) _clouds.Density = Smog;
    }

    private void OnPlayerCrashed() => End(Result.Crash);

    private void End(Result result)
    {
        if (IsOver) return;

        Outcome = result;
        // El dron se queda flotando en su sitio (salvo tras un choque, que ya lo hace caer)
        if (_input != null) _input.Locked = true;
        Ended?.Invoke(result);
    }

    private void HandleDebugKeys()
    {
        if (!Debug.isDebugBuild || Keyboard.current == null) return;
        if (Keyboard.current.kKey.wasPressedThisFrame) ReduceSmog(_debugStep);
        if (Keyboard.current.lKey.wasPressedThisFrame) AddSmog(_debugStep);
    }
}
