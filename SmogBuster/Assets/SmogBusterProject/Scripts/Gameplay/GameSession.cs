using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reglas de la partida (GDD 5.2 y 5.3): nivel global de smog, temporizador y fin de partida.
/// - El smog empieza en 60% y sube 1% cada 2 s.
/// - Victoria: smog al 0% antes de que acabe el tiempo.
/// - Derrota: smog al 100%, tiempo a 00:00 o choque contra un edificio.
/// Usa tiempo escalado, así que se detiene con la pausa (timeScale = 0).
/// También lleva el puntaje y las estadísticas de impacto (GDD 9.3): nubes disipadas, árboles plantados
/// y smog eliminado, con un bonus por el tiempo que sobra al ganar.
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

    [Header("Puntaje")]
    [SerializeField] private int _cloudPoints = 100;
    [SerializeField] private int _microCloudPoints = 50;
    [SerializeField] private int _treePoints = 250;
    [Tooltip("Puntos por cada segundo que sobra al ganar.")]
    [SerializeField] private int _timeBonusPerSecond = 10;

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

    public int Score { get; private set; }
    public int CloudsPurified { get; private set; }
    public int TreesPlanted { get; private set; }
    /// <summary>Puntos de siembra del nivel (sembrados o no).</summary>
    public int TotalPlantingSpots { get; private set; }
    /// <summary>Smog total que el jugador ha quitado (0-1 por cada 100%), aunque luego haya vuelto a subir.</summary>
    public float SmogRemoved { get; private set; }
    /// <summary>Puntos del bonus por tiempo (solo al ganar).</summary>
    public int TimeBonus { get; private set; }

    private const string BestScorePrefix = "best.";

    /// <summary>Mejor puntaje guardado de un nivel (nombre de escena), 0 si no se ha ganado.</summary>
    public static int BestScore(string sceneName) => PlayerPrefs.GetInt(BestScorePrefix + sceneName, 0);

    /// <summary>True si esta partida superó el mejor puntaje del nivel.</summary>
    public bool IsNewBest { get; private set; }

    /// <summary>Se lanza cuando cambian el puntaje o las estadísticas.</summary>
    public event Action StatsChanged;

    /// <summary>Se lanza una sola vez, al terminar la partida.</summary>
    public event Action<Result> Ended;

    private void Awake()
    {
        Instance = this;

        // Dificultad de Opciones: el smog sube más o menos rápido y hay más o menos tiempo
        GameSettings.DifficultyLevel difficulty = GameSettings.Difficulty;
        _riseInterval *= GameSettings.SmogIntervalMultiplier(difficulty);
        _timeLimit = Mathf.Max(30f, _timeLimit + GameSettings.ExtraTime(difficulty));

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

        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnTreePlanted;
    }

    private void Start()
    {
        // Los puntos de siembra se registran en su OnEnable, antes de Start
        TotalPlantingSpots = PlantingSpot.Available.Count + TreesPlanted;
        StatsChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnTreePlanted;
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
        float before = Smog;
        SetSmog(Smog - amount);
        SmogRemoved += before - Smog;
        // Se gana en el acto: si se esperase al Update, la subida periódica podría adelantarse y quitar la victoria
        if (Smog <= 0f) End(Result.Victory);
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

    private void OnCloudPurified(SmogCloud cloud)
    {
        if (IsOver) return;
        CloudsPurified++;
        Score += cloud.IsMicro ? _microCloudPoints : _cloudPoints;
        StatsChanged?.Invoke();
    }

    private void OnTreePlanted(PlantingSpot spot)
    {
        if (IsOver) return;
        TreesPlanted++;
        Score += _treePoints;
        StatsChanged?.Invoke();
    }

    private void End(Result result)
    {
        if (IsOver) return;

        Outcome = result;
        if (result == Result.Victory)
        {
            TimeBonus = Mathf.CeilToInt(TimeRemaining) * _timeBonusPerSecond;
            Score += TimeBonus;
            SaveBestScore();
            StatsChanged?.Invoke();
        }
        // El dron se queda flotando en su sitio (salvo tras un choque, que ya lo hace caer)
        if (_input != null) _input.Locked = true;
        Ended?.Invoke(result);
    }

    private void SaveBestScore()
    {
        string key = BestScorePrefix + gameObject.scene.name;
        if (Score <= PlayerPrefs.GetInt(key, 0)) return;
        PlayerPrefs.SetInt(key, Score);
        PlayerPrefs.Save();
        IsNewBest = true;
    }

    private void HandleDebugKeys()
    {
        if (!Debug.isDebugBuild || Keyboard.current == null) return;
        if (Keyboard.current.kKey.wasPressedThisFrame) ReduceSmog(_debugStep);
        if (Keyboard.current.lKey.wasPressedThisFrame) AddSmog(_debugStep);
    }
}
