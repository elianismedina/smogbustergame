using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD con la barra de nivel de smog. Lee <see cref="GameSession.Smog"/> (0-1) y la muestra
/// con relleno animado, porcentaje y color (verde = limpio, marrón rojizo = muy contaminado).
/// Por encima del umbral crítico el borde parpadea.
/// Sin GameSession usa <see cref="SmogClouds.Density"/>; también se puede alimentar con <see cref="SetLevel"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SmogMeterController : MonoBehaviour
{
    [Tooltip("Fuente del nivel. Si se deja vacío, se busca en la escena.")]
    [SerializeField] private GameSession _session;
    [Tooltip("Fuente alternativa si la escena no tiene GameSession.")]
    [SerializeField] private SmogClouds _source;
    [Tooltip("Velocidad con la que la barra alcanza el valor real.")]
    [SerializeField] private float _smoothing = 4f;
    [Range(0f, 1f)]
    [SerializeField] private float _criticalThreshold = 0.75f;
    [SerializeField] private float _pulseInterval = 0.5f;
    [SerializeField] private Gradient _colors = DefaultGradient();

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _meter;
    private VisualElement _fill;
    private Label _value;
    private IVisualElementScheduledItem _pulse;
    private Rect _lastSafeArea;

    private float _target;
    private float _displayed = -1f;
    private int _shownPercent = -1;

    /// <summary>Nivel mostrado como objetivo (0-1).</summary>
    public float Level => _target;

    /// <summary>Fija el nivel manualmente (ignora la fuente).</summary>
    public void SetLevel(float level)
    {
        _session = null;
        _source = null;
        _target = Mathf.Clamp01(level);
    }

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _safeArea = _root.Q<VisualElement>("hud-safe-area");
        _meter = _root.Q<VisualElement>("smog-meter");
        _fill = _root.Q<VisualElement>("smog-meter-fill");
        _value = _root.Q<Label>("smog-meter-value");

        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_session == null && _source == null) _source = FindAnyObjectByType<SmogClouds>();
        ReadSource();

        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        _pulse = _meter?.schedule.Execute(() => _meter.ToggleInClassList("smog-meter--pulse"))
            .Every((long)(_pulseInterval * 1000));
    }

    private void OnDisable()
    {
        _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        _pulse?.Pause();
    }

    private void Update()
    {
        ReadSource();
        if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        if (_fill == null) return;

        // Primer frame: sin animación
        _displayed = _displayed < 0f
            ? _target
            : Mathf.Lerp(_displayed, _target, 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime));

        _fill.style.width = Length.Percent(_displayed * 100f);
        _fill.style.backgroundColor = _colors.Evaluate(_displayed);

        int percent = Mathf.RoundToInt(_displayed * 100f);
        if (percent != _shownPercent && _value != null)
        {
            _shownPercent = percent;
            _value.text = percent + "%";
        }

        bool critical = _displayed >= _criticalThreshold;
        _meter.EnableInClassList("smog-meter--critical", critical);
        if (!critical) _meter.RemoveFromClassList("smog-meter--pulse");
    }

    private void ReadSource()
    {
        if (_session != null) _target = _session.Smog;
        else if (_source != null) _target = _source.Density;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt) => ApplySafeArea();

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;
        if (_safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;

        // Píxeles de pantalla -> unidades del panel (PanelSettings escala la UI)
        float scaleX = _root.layout.width / Screen.width;
        float scaleY = _root.layout.height / Screen.height;
        if (float.IsNaN(scaleX) || float.IsNaN(scaleY)) return;

        Rect area = Screen.safeArea;
        _safeArea.style.left = area.xMin * scaleX;
        _safeArea.style.right = (Screen.width - area.xMax) * scaleX;
        _safeArea.style.top = (Screen.height - area.yMax) * scaleY;
        _safeArea.style.bottom = area.yMin * scaleY;
    }

    private static Gradient DefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.47f, 0.90f, 0.67f), 0f),   // aire limpio
                new GradientColorKey(new Color(0.90f, 0.80f, 0.35f), 0.5f), // contaminado
                new GradientColorKey(new Color(0.80f, 0.40f, 0.25f), 1f),   // muy contaminado
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
