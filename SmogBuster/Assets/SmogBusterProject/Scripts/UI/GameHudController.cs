using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// HUD de juego: crea los joysticks táctiles y pasa sus valores a <see cref="QuadInput"/>.
/// - Stick izquierdo: mover (adelante/atrás, lateral).
/// - Stick derecho: vertical = subir/bajar, horizontal = girar.
/// Ajusta el área segura (notch) y oculta los sticks en dispositivos sin pantalla táctil.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameHudController : MonoBehaviour
{
    [SerializeField] private QuadInput _input;
    [Tooltip("Mostrar los sticks también en escritorio (útil para probar con el ratón).")]
    [SerializeField] private bool _showSticksOnDesktop;
    [SerializeField] private float _stickRadius = 110f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _deadZone = 0.12f;

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _touchZones;
    private VirtualJoystick _moveStick;
    private VirtualJoystick _altitudeStick;
    private QuadcopterCrash _crash;
    private Rect _lastSafeArea;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _safeArea = _root.Q<VisualElement>("game-hud-safe-area");
        _touchZones = _root.Q<VisualElement>("touch-zones");

        _moveStick = CreateStick("touch-zone-left", "joystick--move");
        _altitudeStick = CreateStick("touch-zone-right", "joystick--altitude");

        if (_input == null) _input = FindAnyObjectByType<QuadInput>();
        _crash = _input != null ? _input.GetComponent<QuadcopterCrash>() : null;

        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnDisable()
    {
        _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        ReleaseSticks();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        if (_input == null) return;

        // Tras el choque los sticks no hacen nada y se ocultan
        bool crashed = _crash != null && _crash.HasCrashed;
        SetSticksVisible(!crashed && HasTouchInput());
        if (crashed) return;

        Vector2 move = _moveStick?.Value ?? Vector2.zero;
        Vector2 altitude = _altitudeStick?.Value ?? Vector2.zero;
        _input.SetMove(move);
        _input.SetClimb(altitude.y);
        _input.SetYaw(altitude.x);
    }

    // Móvil real, Device Simulator (crea un Touchscreen) o PC táctil
    private bool HasTouchInput() => Application.isMobilePlatform || Touchscreen.current != null || _showSticksOnDesktop;

    private void SetSticksVisible(bool visible)
    {
        if (_touchZones == null) return;
        DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_touchZones.style.display == wanted) return;
        if (!visible) ReleaseSticks();
        _touchZones.style.display = wanted;
    }

    /// <summary>Suelta ambos sticks y pone la entrada externa a 0 (p. ej. al pausar).</summary>
    public void ReleaseSticks()
    {
        _moveStick?.Release();
        _altitudeStick?.Release();
        if (_input != null)
        {
            _input.SetMove(Vector2.zero);
            _input.SetClimb(0f);
            _input.SetYaw(0f);
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
}
