using UnityEngine;

/// <summary>
/// Controla las nubes de smog a ras de calle (un ParticleSystem de manchas grandes y suaves).
/// <see cref="Density"/> (0-1) escala cuántas nubes hay y lo opacas que son; bajarla
/// "limpia" el aire de forma gradual (p. ej. cuando el jugador elimina smog).
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class SmogClouds : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float _density = 0.8f;

    [Tooltip("Número de nubes con densidad 1.")]
    [SerializeField] private int _maxClouds = 75;

    [Tooltip("Opacidad de cada nube con densidad 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxAlpha = 0.65f;

    [SerializeField] private Color _colorA = new Color(0.66f, 0.61f, 0.48f);
    [SerializeField] private Color _colorB = new Color(0.55f, 0.52f, 0.42f);

    private ParticleSystem _particles;

    public float Density
    {
        get => _density;
        set
        {
            _density = Mathf.Clamp01(value);
            Apply();
        }
    }

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        Apply();
    }

    private void OnValidate()
    {
        _particles = GetComponent<ParticleSystem>();
        Apply();
    }

    private void Apply()
    {
        if (_particles == null) return;

        ParticleSystem.MainModule main = _particles.main;
        int count = Mathf.RoundToInt(_maxClouds * _density);
        main.maxParticles = Mathf.Max(1, count);

        // Más densidad = nubes más opacas (raíz para que a densidad media se sigan viendo)
        float alpha = _maxAlpha * Mathf.Sqrt(_density);
        Color a = _colorA; a.a = alpha;
        Color b = _colorB; b.a = alpha * 0.8f;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);

        // Emisión estable: reponer las nubes que expiran
        float lifetime = Mathf.Max(1f, (main.startLifetime.constantMin + main.startLifetime.constantMax) * 0.5f);
        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.rateOverTime = count / lifetime;
        emission.enabled = count > 0;
    }
}
