using UnityEngine;

/// <summary>
/// Fuente de contaminación (chimenea o tubo de escape, GDD 8.1 y 8.4): suelta una micro-nube
/// cada 10 a 15 s mientras dure la partida, sin pasar del límite de nubes activas.
/// </summary>
public class SmogEmitter : MonoBehaviour
{
    [SerializeField] private SmogCloud _microCloudPrefab;
    [SerializeField] private Vector2 _interval = new Vector2(10f, 15f);
    [Tooltip("Retraso antes de la primera emisión (para que no salgan todas a la vez).")]
    [SerializeField] private Vector2 _firstDelay = new Vector2(4f, 12f);
    [Tooltip("Límite de nubes activas en toda la escena (rendimiento en móvil, GDD 11.3).")]
    [SerializeField] private int _maxActiveClouds = 16;
    [Tooltip("Punto de salida respecto al emisor.")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0f);

    private float _timer;

    private void Start()
    {
        _timer = Random.Range(_firstDelay.x, _firstDelay.y);
    }

    private void Update()
    {
        if (_microCloudPrefab == null) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = Random.Range(_interval.x, _interval.y);
        if (SmogCloud.Active.Count >= _maxActiveClouds) return;

        Instantiate(_microCloudPrefab, transform.position + _spawnOffset, Quaternion.identity);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.5f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + _spawnOffset, 0.6f);
    }
}
