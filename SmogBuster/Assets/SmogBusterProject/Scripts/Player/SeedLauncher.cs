using System;
using UnityEngine;

/// <summary>
/// Lanzador de Semillas Instantáneas (GDD 6.1): al pulsar Semilla, dispara una cápsula desde el cañón verde.
/// Busca el punto de siembra libre más cercano delante del dron (<see cref="CurrentTarget"/>) y lanza la
/// semilla en parábola hacia él; si no hay ninguno, la semilla cae delante y marca el fallo.
/// No se puede usar a la vez que el Rayo (GDD 7.5): mientras el Rayo dispara no se lanza, y justo
/// después de lanzar el Rayo queda bloqueado un momento (<see cref="IsBusy"/>).
/// </summary>
[RequireComponent(typeof(QuadInput))]
public class SeedLauncher : MonoBehaviour
{
    [Tooltip("Punta del cañón verde. Si se deja vacío, se busca un hijo llamado Cannon_Seed.")]
    [SerializeField] private Transform _muzzle;
    [SerializeField] private SeedProjectile _seedPrefab;
    [Tooltip("Distancia máxima a un punto de siembra.")]
    [SerializeField] private float _range = 35f;
    [Tooltip("Grados a cada lado del frente del dron dentro de los que se busca punto de siembra.")]
    [SerializeField] private float _aimAngle = 35f;
    [SerializeField] private float _cooldown = 0.6f;
    [Tooltip("Tiempo durante el que el Rayo queda bloqueado tras lanzar (no se usan a la vez).")]
    [SerializeField] private float _busyTime = 0.35f;
    [SerializeField] private float _flightTime = 0.9f;
    [Tooltip("Hasta dónde cae una semilla lanzada sin punto de siembra.")]
    [SerializeField] private float _missDistance = 14f;

    private QuadInput _input;
    private QuadcopterCrash _crash;
    private PurifierBeam _beam;
    private bool _wasPressed;
    private float _nextLaunchTime;
    private float _busyUntil;

    /// <summary>Punto de siembra al que iría la semilla si se lanzase ahora.</summary>
    public PlantingSpot CurrentTarget { get; private set; }

    /// <summary>Se lanza al disparar una semilla (para sonido y efectos).</summary>
    public event Action Launched;

    /// <summary>True justo después de lanzar: el Rayo no puede dispararse.</summary>
    public bool IsBusy => Time.time < _busyUntil;

    private void Awake()
    {
        _input = GetComponent<QuadInput>();
        _crash = GetComponent<QuadcopterCrash>();
        _beam = GetComponent<PurifierBeam>();
        if (_muzzle == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cannon_Seed") _muzzle = t;
            }
        }
    }

    private void OnDisable() => SetTarget(null);

    private void Update()
    {
        bool crashed = _crash != null && _crash.HasCrashed;
        bool over = GameSession.Instance != null && GameSession.Instance.IsOver;
        bool active = !crashed && !over && Time.timeScale > 0f;

        SetTarget(active ? FindTarget() : null);

        // Se lanza al pulsar (no al mantener)
        bool pressed = _input.Seed;
        bool justPressed = pressed && !_wasPressed;
        _wasPressed = pressed;

        bool beamFiring = _beam != null && _beam.IsFiring;
        if (!active || !justPressed || beamFiring || Time.time < _nextLaunchTime) return;
        Launch();
    }

    private void Launch()
    {
        _nextLaunchTime = Time.time + _cooldown;
        _busyUntil = Time.time + _busyTime;
        if (_seedPrefab == null) return;

        Vector3 start = _muzzle != null ? _muzzle.position : transform.position;
        PlantingSpot target = CurrentTarget;
        Vector3 end = target != null ? target.AimPoint : MissPoint(start);

        SeedProjectile seed = Instantiate(_seedPrefab, start, Quaternion.identity);
        seed.Launch(start, end, _flightTime, target);
        SetTarget(null);
        Launched?.Invoke();
    }

    private PlantingSpot FindTarget()
    {
        Vector3 origin = transform.position;
        Vector3 forward = Flat(transform.forward);
        PlantingSpot best = null;
        float bestScore = float.MaxValue;

        foreach (PlantingSpot spot in PlantingSpot.Available)
        {
            Vector3 toSpot = spot.AimPoint - origin;
            float distance = toSpot.magnitude;
            if (distance > _range) continue;

            float angle = Vector3.Angle(forward, Flat(toSpot));
            // Muy cerca (justo debajo del dron) cuenta aunque el ángulo horizontal sea grande
            if (angle > _aimAngle && Flat(toSpot).magnitude > 4f) continue;

            // Preferir lo que está delante y cerca
            float score = distance * (1f + angle / _aimAngle);
            if (score < bestScore)
            {
                bestScore = score;
                best = spot;
            }
        }
        return best;
    }

    private Vector3 MissPoint(Vector3 start)
    {
        Vector3 forward = Flat(transform.forward);
        Vector3 probe = start + forward * _missDistance;
        if (Physics.Raycast(probe + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return new Vector3(probe.x, 0f, probe.z);
    }

    private void SetTarget(PlantingSpot target)
    {
        if (target == CurrentTarget) return;
        if (CurrentTarget != null) CurrentTarget.SetTargeted(false);
        CurrentTarget = target;
        if (target != null) target.SetTargeted(true);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
