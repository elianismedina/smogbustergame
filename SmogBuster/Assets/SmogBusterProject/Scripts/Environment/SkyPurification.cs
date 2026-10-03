using UnityEngine;

/// <summary>
/// El cielo se limpia a medida que baja el smog (GDD 8.6 y 9.1).
/// Mezcla el skybox con smog con el cielo limpio (shader SmogBuster/Skybox/Panoramic Blend)
/// y lleva la niebla, la luz ambiente y el sol hacia sus valores de aire limpio.
/// Los valores con smog son los que la escena tiene al empezar.
/// </summary>
public class SkyPurification : MonoBehaviour
{
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    [SerializeField] private GameSession _session;

    [Header("Umbrales de smog (0-1)")]
    [Tooltip("Por encima de este nivel el cielo se ve totalmente contaminado.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dirtySmog = 0.5f;
    [Tooltip("Por debajo de este nivel el cielo se ve totalmente limpio.")]
    [Range(0f, 1f)]
    [SerializeField] private float _cleanSmog = 0.05f;
    [Tooltip("Velocidad con la que el cielo sigue al nivel de smog.")]
    [SerializeField] private float _smoothing = 1.5f;

    [Header("Aire limpio")]
    [SerializeField] private Color _cleanFogColor = new Color(0.74f, 0.80f, 0.86f);
    [SerializeField] private float _cleanFogDensity = 0.004f;
    [SerializeField] private Color _cleanAmbientSky = new Color(0.62f, 0.74f, 0.92f);
    [SerializeField] private Color _cleanAmbientEquator = new Color(0.72f, 0.70f, 0.64f);
    [SerializeField] private Color _cleanAmbientGround = new Color(0.30f, 0.29f, 0.25f);

    [Header("Sol (opcional)")]
    [SerializeField] private Light _sun;
    [SerializeField] private Color _cleanSunColor = new Color(1f, 0.90f, 0.76f);
    [SerializeField] private float _cleanSunIntensity = 1.5f;

    private Material _sky;
    private bool _ownsSky;
    private Material _originalSky;

    private Color _dirtyFogColor;
    private float _dirtyFogDensity;
    private Color _dirtyAmbientSky;
    private Color _dirtyAmbientEquator;
    private Color _dirtyAmbientGround;
    private Color _dirtySunColor;
    private float _dirtySunIntensity;

    private float _blend = -1f;

    /// <summary>Cuánto se ha limpiado el cielo (0 = con smog, 1 = limpio).</summary>
    public float Blend => Mathf.Max(0f, _blend);

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_sun == null) _sun = RenderSettings.sun;

        // Usar la copia del skybox del SkyboxRotator, o crear una para no tocar el asset
        SkyboxRotator rotator = FindAnyObjectByType<SkyboxRotator>();
        _sky = rotator != null ? rotator.Instance : null;
        if (_sky == null && RenderSettings.skybox != null)
        {
            _originalSky = RenderSettings.skybox;
            _sky = new Material(_originalSky);
            _ownsSky = true;
            RenderSettings.skybox = _sky;
        }

        if (_sky != null && !_sky.HasProperty(BlendId))
        {
            Debug.LogWarning("SkyPurification: el skybox no usa el shader Panoramic Blend; solo cambiarán la niebla y la luz.", this);
            _sky = null;
        }

        _dirtyFogColor = RenderSettings.fogColor;
        _dirtyFogDensity = RenderSettings.fogDensity;
        _dirtyAmbientSky = RenderSettings.ambientSkyColor;
        _dirtyAmbientEquator = RenderSettings.ambientEquatorColor;
        _dirtyAmbientGround = RenderSettings.ambientGroundColor;
        if (_sun != null)
        {
            _dirtySunColor = _sun.color;
            _dirtySunIntensity = _sun.intensity;
        }
    }

    private void Update()
    {
        if (_session == null) return;

        float target = Mathf.InverseLerp(_dirtySmog, _cleanSmog, _session.Smog);
        // Primer frame: sin transición
        _blend = _blend < 0f
            ? target
            : Mathf.Lerp(_blend, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));

        Apply(_blend);
    }

    private void Apply(float t)
    {
        if (_sky != null) _sky.SetFloat(BlendId, t);

        RenderSettings.fogColor = Color.Lerp(_dirtyFogColor, _cleanFogColor, t);
        RenderSettings.fogDensity = Mathf.Lerp(_dirtyFogDensity, _cleanFogDensity, t);
        RenderSettings.ambientSkyColor = Color.Lerp(_dirtyAmbientSky, _cleanAmbientSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(_dirtyAmbientEquator, _cleanAmbientEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(_dirtyAmbientGround, _cleanAmbientGround, t);

        if (_sun != null)
        {
            _sun.color = Color.Lerp(_dirtySunColor, _cleanSunColor, t);
            _sun.intensity = Mathf.Lerp(_dirtySunIntensity, _cleanSunIntensity, t);
        }
    }

    private void OnDestroy()
    {
        // Dejar la escena como estaba (importa al salir de Play Mode en el Editor)
        if (_blend >= 0f) Apply(0f);
        if (!_ownsSky || _sky == null) return;
        if (RenderSettings.skybox == _sky) RenderSettings.skybox = _originalSky;
        Destroy(_sky);
    }
}
