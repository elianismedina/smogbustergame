using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nube de smog individual (GDD 8.1 y 8.4). El Rayo Purificador la disipa al instante y baja el smog global.
/// - Nube grande: está quieta sobre avenidas y fábricas; al disiparla, −5%.
/// - Micro-nube (la sueltan chimeneas): viaja despacio y, si nadie la neutraliza a tiempo,
///   se disuelve en el aire y sube el smog.
/// Necesita un collider en modo trigger para que el rayo la detecte.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SmogCloud : MonoBehaviour
{
    private static readonly List<SmogCloud> ActiveList = new List<SmogCloud>();

    [Tooltip("Cuánto baja el smog global al disiparla (0.05 = 5%).")]
    [SerializeField] private float _purifyAmount = 0.05f;

    [Header("Micro-nube")]
    [SerializeField] private bool _isMicro;
    [Tooltip("Metros por segundo.")]
    [SerializeField] private float _driftSpeed = 0.8f;
    [Tooltip("Segundos hasta que se disuelve en el aire si no se neutraliza.")]
    [SerializeField] private float _settleTime = 15f;
    [Tooltip("Cuánto sube el smog global al disolverse sin neutralizar.")]
    [SerializeField] private float _settleAmount = 0.02f;
    [Tooltip("Escala final respecto a la inicial (crece mientras viaja).")]
    [SerializeField] private float _growth = 2f;

    [Header("Desaparición")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private Collider _collider;
    private ParticleSystem[] _particles;
    private Vector3 _driftDirection;
    private Vector3 _startScale;
    private float _age;
    private bool _gone;

    /// <summary>Nubes que siguen en el aire.</summary>
    public static IReadOnlyList<SmogCloud> Active => ActiveList;

    public bool IsMicro => _isMicro;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _particles = GetComponentsInChildren<ParticleSystem>();
        _startScale = transform.localScale;

        Vector2 flat = Random.insideUnitCircle.normalized;
        _driftDirection = new Vector3(flat.x, 0.15f, flat.y).normalized;
    }

    private void OnEnable() => ActiveList.Add(this);

    private void OnDisable() => ActiveList.Remove(this);

    private void Update()
    {
        if (_gone || !_isMicro) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        _age += Time.deltaTime;
        transform.position += _driftDirection * (_driftSpeed * Time.deltaTime);
        transform.localScale = _startScale * Mathf.Lerp(1f, _growth, _age / _settleTime);

        if (_age >= _settleTime)
        {
            GameSession.Instance?.AddSmog(_settleAmount);
            Disappear();
        }
    }

    /// <summary>La disipa el Rayo Purificador: baja el smog y la nube se desvanece.</summary>
    public void Purify()
    {
        if (_gone) return;
        GameSession.Instance?.ReduceSmog(_purifyAmount);
        Disappear();
    }

    private void Disappear()
    {
        _gone = true;
        _collider.enabled = false;
        ActiveList.Remove(this);
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        foreach (ParticleSystem ps in _particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Las partículas vivas se encogen y se van: la nube "se deshace" en lugar de cortarse de golpe
        Vector3 from = transform.localScale;
        for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(from, from * 0.2f, t / _fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
