using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de siembra (GDD 5.2, 8.1): maceta, parterre o techo verde con un haz de luz verde que lo señala.
/// Al recibir una Semilla Instantánea, la planta crece al instante y purifica el aire de forma pasiva
/// (0.2% de smog por segundo; ver _purifyPerSecond) durante el resto de la partida. Cada punto se siembra una sola vez.
/// </summary>
public class PlantingSpot : MonoBehaviour
{
    private static readonly List<PlantingSpot> AvailableList = new List<PlantingSpot>();

    [Tooltip("Haz de luz e indicadores que se apagan al sembrar.")]
    [SerializeField] private GameObject _indicator;
    [Tooltip("Planta que crece al sembrar (empieza oculta).")]
    [SerializeField] private Transform _plant;
    [Tooltip("Halo que se enciende cuando el punto está en la mira de la semilla.")]
    [SerializeField] private GameObject _highlight;
    [SerializeField] private float _growDuration = 1.2f;
    [Tooltip("Cuánto baja el smog por segundo una vez crecida la planta (0.002 = 0.2%). El GDD pide 1%, ajustado para que ganar exija combinar semillas y Rayo.")]
    [SerializeField] private float _purifyPerSecond = 0.002f;
    [Tooltip("Altura del punto al que apunta la semilla, sobre el origen.")]
    [SerializeField] private float _aimHeight = 0.4f;

    private Vector3 _plantScale = Vector3.one;
    private bool _grown;
    private bool _targeted;

    /// <summary>Puntos que todavía no se han sembrado.</summary>
    public static IReadOnlyList<PlantingSpot> Available => AvailableList;

    public bool IsPlanted { get; private set; }
    public bool IsTargeted => _targeted;
    public Vector3 AimPoint => transform.position + Vector3.up * _aimHeight;

    private void Awake()
    {
        if (_plant != null)
        {
            _plantScale = _plant.localScale;
            _plant.gameObject.SetActive(false);
        }
        if (_highlight != null) _highlight.SetActive(false);
    }

    private void OnEnable()
    {
        if (!IsPlanted) AvailableList.Add(this);
    }

    private void OnDisable() => AvailableList.Remove(this);

    private void Update()
    {
        if (!_grown) return;
        GameSession session = GameSession.Instance;
        if (session == null || session.IsOver) return;
        session.ReduceSmog(_purifyPerSecond * Time.deltaTime);
    }

    public void SetTargeted(bool targeted)
    {
        if (IsPlanted || targeted == _targeted) return;
        _targeted = targeted;
        if (_highlight != null) _highlight.SetActive(targeted);
    }

    /// <summary>La semilla llega: la planta brota y empieza a purificar.</summary>
    public void Plant()
    {
        if (IsPlanted) return;
        IsPlanted = true;
        _targeted = false;
        AvailableList.Remove(this);
        if (_highlight != null) _highlight.SetActive(false);
        if (_indicator != null) _indicator.SetActive(false);
        StartCoroutine(Grow());
    }

    private IEnumerator Grow()
    {
        if (_plant != null)
        {
            _plant.gameObject.SetActive(true);
            for (float t = 0f; t < _growDuration; t += Time.deltaTime)
            {
                // Crecimiento con un pequeño rebote al final: se siente como «¡brotó!»
                float k = t / _growDuration;
                float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
                _plant.localScale = _plantScale * (k * overshoot);
                yield return null;
            }
            _plant.localScale = _plantScale;
        }
        _grown = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(AimPoint, 0.6f);
    }
}
