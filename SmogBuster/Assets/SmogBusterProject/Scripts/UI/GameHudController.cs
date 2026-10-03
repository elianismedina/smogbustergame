using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// HUD de juego (GDD 6.2 y 9.3): un joystick táctil para moverse y botones que se mantienen
/// pulsados para subir, bajar y disparar el Rayo. Pasa sus valores a <see cref="QuadInput"/>.
/// Muestra el tiempo restante de <see cref="GameSession"/> (MM:SS).
/// Ajusta el área segura (notch) y oculta los controles táctiles en dispositivos sin pantalla táctil.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameHudController : MonoBehaviour
{
    [SerializeField] private QuadInput _input;
    [Tooltip("Mostrar los controles táctiles también en escritorio (útil para probar con el ratón).")]
    [SerializeField] private bool _showSticksOnDesktop;
    [SerializeField] private float _stickRadius = 110f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _deadZone = 0.12f;
    [Tooltip("Segundos restantes a partir de los que el temporizador se marca en rojo.")]
    [SerializeField] private float _timerWarning = 30f;
    [Header("Borde del mapa (GDD 5.4)")]
    [Tooltip("Mitad del lado del área de vuelo, centrada en el origen (paredes invisibles de City/Bounds).")]
    [SerializeField] private float _playAreaHalfSize = 77.5f;
    [Tooltip("A esta distancia de la pared aparece «Fuera de alcance».")]
    [SerializeField] private float _boundaryMargin = 6f;

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _touchControls;
    private VirtualJoystick _moveStick;
    private HoldButton _upButton;
    private HoldButton _downButton;
    private HoldButton _beamButton;
    private HoldButton _seedButton;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private Label _timer;
    private Label _boundary;
    private VisualElement _crosshair;
    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private VisualElement _seedElement;
    private IVisualElementScheduledItem _seedPulse;
    private VisualElement _beamElement;
    private IVisualElementScheduledItem _beamPulse;
    private int _shownSeconds = -1;
    private Rect _lastSafeArea;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _safeArea = _root.Q<VisualElement>("game-hud-safe-area");
        _touchControls = _root.Q<VisualElement>("touch-controls");
        _timer = _root.Q<Label>("hud-timer");
        _boundary = _root.Q<Label>("hud-boundary");
        _crosshair = _root.Q<VisualElement>("crosshair");

        _moveStick = CreateStick("touch-zone-left", "joystick--move");
        _upButton = new HoldButton(_root.Q<VisualElement>("btn-up"));
        _downButton = new HoldButton(_root.Q<VisualElement>("btn-down"));
        _beamElement = _root.Q<VisualElement>("btn-beam");
        _beamButton = new HoldButton(_beamElement);
        _seedElement = _root.Q<VisualElement>("btn-seed");
        _seedButton = new HoldButton(_seedElement);
        _seedPulse = _seedElement?.schedule.Execute(() => _seedElement.ToggleInClassList("action-button--pulse")).Every(350);
        // Con una nube en la mira el botón del Rayo late, para invitar a disparar
        _beamPulse = _beamElement?.schedule.Execute(() => _beamElement.ToggleInClassList("action-button--pulse")).Every(350);

        if (_input == null) _input = FindAnyObjectByType<QuadInput>();
        _crash = _input != null ? _input.GetComponent<QuadcopterCrash>() : null;
        _beam = _input != null ? _input.GetComponent<PurifierBeam>() : null;
        _seeds = _input != null ? _input.GetComponent<SeedLauncher>() : null;
        _session = FindAnyObjectByType<GameSession>();
        if (_timer != null) _timer.style.display = _session != null ? DisplayStyle.Flex : DisplayStyle.None;

        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnDisable()
    {
        _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        ReleaseSticks();
        _beamPulse?.Pause();
        _seedPulse?.Pause();
        _seedButton?.Dispose();
        _upButton?.Dispose();
        _downButton?.Dispose();
        _beamButton?.Dispose();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        UpdateTimer();
        UpdateBeamReady();
        UpdateBoundary();
        UpdateCrosshair();
        if (_input == null) return;

        // Al terminar la partida (o tras el choque) los controles no hacen nada y se ocultan
        bool over = (_crash != null && _crash.HasCrashed) || (_session != null && _session.IsOver);
        SetSticksVisible(!over && HasTouchInput());
        if (over) return;

        _input.SetMove(_moveStick?.Value ?? Vector2.zero);
        float climb = (_upButton.IsPressed ? 1f : 0f) - (_downButton.IsPressed ? 1f : 0f);
        _input.SetClimb(climb);
        _input.SetBeam(_beamButton.IsPressed);
        _input.SetSeed(_seedButton.IsPressed);
    }

    // La mira marca hacia dónde va el Rayo; se pone cian cuando hay una nube fijada
    private void UpdateCrosshair()
    {
        if (_crosshair == null) return;
        Camera cam = Camera.main;
        bool over = (_crash != null && _crash.HasCrashed) || (_session != null && _session.IsOver);
        if (_beam == null || cam == null || over || _crosshair.panel == null)
        {
            _crosshair.style.display = DisplayStyle.None;
            return;
        }

        Vector3 aimPoint = _beam.AimOrigin + _beam.AimDirection * (_beam.Range * 0.6f);
        Vector3 screen = cam.WorldToScreenPoint(aimPoint);
        if (screen.z <= 0f)
        {
            _crosshair.style.display = DisplayStyle.None;
            return;
        }

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_crosshair.panel, new Vector2(screen.x, Screen.height - screen.y));
        _crosshair.style.display = DisplayStyle.Flex;
        _crosshair.style.left = panelPos.x;
        _crosshair.style.top = panelPos.y;
        _crosshair.EnableInClassList("crosshair--locked", _beam.CurrentTarget != null);
    }

    private void UpdateBoundary()
    {
        if (_boundary == null) return;
        bool near = false;
        if (_input != null && (_session == null || !_session.IsOver))
        {
            Vector3 p = _input.transform.position;
            float limit = _playAreaHalfSize - _boundaryMargin;
            near = Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit;
        }
        _boundary.EnableInClassList("hud-boundary--visible", near);
    }

    private void UpdateBeamReady()
    {
        SetReady(_beamElement, _beam != null && _beam.CurrentTarget != null);
        SetReady(_seedElement, _seeds != null && _seeds.CurrentTarget != null);
    }

    // Con un objetivo a tiro el botón late, para invitar a pulsarlo
    private static void SetReady(VisualElement button, bool ready)
    {
        if (button == null) return;
        button.EnableInClassList("action-button--ready", ready);
        if (!ready) button.RemoveFromClassList("action-button--pulse");
    }

    private void UpdateTimer()
    {
        if (_timer == null || _session == null) return;

        // Redondeo hacia arriba: muestra 00:00 solo cuando el tiempo se ha agotado
        int seconds = Mathf.CeilToInt(_session.TimeRemaining);
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        _timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
        _timer.EnableInClassList("hud-timer--warning", seconds <= _timerWarning);
    }

    // Móvil real, Device Simulator (crea un Touchscreen) o PC táctil
    private bool HasTouchInput() => Application.isMobilePlatform || Touchscreen.current != null || _showSticksOnDesktop;

    private void SetSticksVisible(bool visible)
    {
        if (_touchControls == null) return;
        DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_touchControls.style.display == wanted) return;
        if (!visible) ReleaseSticks();
        _touchControls.style.display = wanted;
    }

    /// <summary>Suelta el joystick y los botones y pone la entrada externa a 0 (p. ej. al pausar).</summary>
    public void ReleaseSticks()
    {
        _moveStick?.Release();
        _upButton?.Release();
        _downButton?.Release();
        _beamButton?.Release();
        _seedButton?.Release();
        if (_input != null)
        {
            _input.SetMove(Vector2.zero);
            _input.SetClimb(0f);
            _input.SetBeam(false);
            _input.SetSeed(false);
        }
    }

    private VirtualJoystick CreateStick(string zoneName, string modifierClass)
    {
        VisualElement zone = _root.Q<VisualElement>(zoneName);
        if (zone == null) return null;

        zone.Clear();
        var stick = new VirtualJoystick { Radius = _stickRadius, DeadZone = _deadZone };
        stick.AddToClassList(modifierClass);
        zone.Add(stick);
        return stick;
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

    /// <summary>
    /// Botón táctil que cuenta mientras se mantiene pulsado. Captura su puntero,
    /// así que funciona a la vez que el joystick (multitouch).
    /// </summary>
    private sealed class HoldButton
    {
        private const string PressedClass = "hud-button--pressed";
        private readonly VisualElement _element;
        private int _pointerId = -1;

        public HoldButton(VisualElement element)
        {
            _element = element;
            if (_element == null) return;
            _element.RegisterCallback<PointerDownEvent>(OnDown);
            _element.RegisterCallback<PointerUpEvent>(OnUp);
            _element.RegisterCallback<PointerCancelEvent>(OnCancel);
        }

        public bool IsPressed => _pointerId >= 0;

        public void Release()
        {
            if (_element != null && _pointerId >= 0 && _element.HasPointerCapture(_pointerId))
            {
                _element.ReleasePointer(_pointerId);
            }
            _pointerId = -1;
            _element?.RemoveFromClassList(PressedClass);
        }

        public void Dispose()
        {
            if (_element == null) return;
            _element.UnregisterCallback<PointerDownEvent>(OnDown);
            _element.UnregisterCallback<PointerUpEvent>(OnUp);
            _element.UnregisterCallback<PointerCancelEvent>(OnCancel);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (_pointerId >= 0) return;
            _pointerId = evt.pointerId;
            _element.CapturePointer(evt.pointerId);
            _element.AddToClassList(PressedClass);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _pointerId) Release();
        }

        private void OnCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _pointerId) Release();
        }
    }
}
