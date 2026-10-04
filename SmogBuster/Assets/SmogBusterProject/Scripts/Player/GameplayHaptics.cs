using UnityEngine;

/// <summary>
/// Vibración en las acciones clave (GDD 10.3), por <see cref="Haptics"/>:
/// - Suave: nube disipada, semilla lanzada.
/// - Media: planta que brota, alerta de smog al 80%, victoria.
/// - Fuerte: choque y derrota.
/// Va en el dron.
/// </summary>
public class GameplayHaptics : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float _alertThreshold = 0.8f;

    private SeedLauncher _seeds;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private bool _aboveThreshold;

    private void Awake()
    {
        _seeds = GetComponent<SeedLauncher>();
        _crash = GetComponent<QuadcopterCrash>();
    }

    private void OnEnable()
    {
        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnPlanted;
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
        if (_crash != null) _crash.Crashed += OnCrashed;
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnPlanted;
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
        if (_crash != null) _crash.Crashed -= OnCrashed;
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    private void Update()
    {
        if (_session == null || _session.IsOver) return;

        // Una sola vibración al cruzar el umbral (el pitido sí se repite)
        bool above = _session.Smog >= _alertThreshold;
        if (above && !_aboveThreshold) Haptics.Pulse(Haptics.Strength.Medium);
        _aboveThreshold = above;
    }

    private void OnCloudPurified(SmogCloud cloud) => Haptics.Pulse(Haptics.Strength.Light);

    private void OnSeedLaunched() => Haptics.Pulse(Haptics.Strength.Light);

    private void OnPlanted(PlantingSpot spot) => Haptics.Pulse(Haptics.Strength.Medium);

    private void OnCrashed() => Haptics.Pulse(Haptics.Strength.Heavy);

    private void OnSessionEnded(GameSession.Result result)
    {
        // El choque ya vibró en OnCrashed
        if (result == GameSession.Result.Crash) return;
        Haptics.Pulse(result == GameSession.Result.Victory ? Haptics.Strength.Medium : Haptics.Strength.Heavy);
    }
}
