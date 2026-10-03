using UnityEngine;

/// <summary>
/// Semilla Instantánea en vuelo: sigue una parábola hasta su destino en un tiempo fijo.
/// Si llega a un punto de siembra libre, lo siembra; si no, deja una X que se desvanece
/// (GDD 10.5: el fallo se señala sin castigar).
/// </summary>
public class SeedProjectile : MonoBehaviour
{
    [Tooltip("Marca de fallo (una X) que aparece donde cae la semilla si no hay punto de siembra.")]
    [SerializeField] private GameObject _missMarkerPrefab;
    [SerializeField] private float _gravity = 18f;
    [SerializeField] private float _spinSpeed = 540f;

    private Vector3 _velocity;
    private float _timeLeft;
    private PlantingSpot _target;

    /// <summary>Lanza la semilla para que llegue a <paramref name="end"/> en <paramref name="flightTime"/> segundos.</summary>
    public void Launch(Vector3 start, Vector3 end, float flightTime, PlantingSpot target)
    {
        transform.position = start;
        _target = target;
        _timeLeft = Mathf.Max(0.1f, flightTime);
        // Velocidad inicial para una parábola que pase por end: d = v·t − ½·g·t²
        _velocity = (end - start) / _timeLeft + 0.5f * _gravity * _timeLeft * Vector3.up;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, _timeLeft);
        _velocity += Vector3.down * (_gravity * dt);
        transform.position += _velocity * dt;
        transform.Rotate(Vector3.right, _spinSpeed * dt, Space.Self);

        _timeLeft -= dt;
        if (_timeLeft > 0f) return;

        if (_target != null && !_target.IsPlanted)
        {
            _target.Plant();
        }
        else if (_missMarkerPrefab != null)
        {
            Instantiate(_missMarkerPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
