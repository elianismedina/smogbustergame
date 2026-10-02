using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Joystick táctil de UI Toolkit. El propio elemento es la zona sensible: al tocarla, la base
/// aparece bajo el dedo (modo flotante) y el mando sigue al dedo hasta <see cref="Radius"/>.
/// Cada joystick captura su propio puntero, así que varios funcionan a la vez (multitouch).
/// </summary>
public class VirtualJoystick : VisualElement
{
    public const string UssClass = "joystick";
    public const string BaseUssClass = "joystick__base";
    public const string KnobUssClass = "joystick__knob";
    public const string ActiveUssClass = "joystick--active";

    private readonly VisualElement _base;
    private readonly VisualElement _knob;
    private int _pointerId = -1;
    private Vector2 _center;

    /// <summary>Recorrido máximo del mando en unidades del panel.</summary>
    public float Radius { get; set; } = 110f;

    /// <summary>Por debajo de esta magnitud la salida es 0.</summary>
    public float DeadZone { get; set; } = 0.12f;

    /// <summary>Si es true, la base se coloca donde empieza el toque.</summary>
    public bool Floating { get; set; } = true;

    /// <summary>Posición de reposo de la base, relativa al tamaño de la zona (0-1).</summary>
    public Vector2 RestAnchor { get; set; } = new Vector2(0.5f, 0.62f);

    /// <summary>Salida en [-1, 1]; +Y es arriba.</summary>
    public Vector2 Value { get; private set; }

    public bool IsActive => _pointerId >= 0;

    public VirtualJoystick()
    {
        AddToClassList(UssClass);

        _base = new VisualElement { pickingMode = PickingMode.Ignore };
        _base.AddToClassList(BaseUssClass);
        _knob = new VisualElement { pickingMode = PickingMode.Ignore };
        _knob.AddToClassList(KnobUssClass);
        _base.Add(_knob);
        Add(_base);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCancelEvent>(_ => Release());
        RegisterCallback<PointerCaptureOutEvent>(_ => Release());
        RegisterCallback<GeometryChangedEvent>(_ => { if (!IsActive) MoveBaseTo(RestPosition()); });
    }

    /// <summary>Suelta el joystick y pone la salida a 0 (p. ej. al pausar).</summary>
    public void Release()
    {
        int id = _pointerId;
        _pointerId = -1; // antes de soltar: ReleasePointer dispara PointerCaptureOut, que vuelve a llamar aquí
        if (id >= 0 && this.HasPointerCapture(id)) this.ReleasePointer(id);
        Value = Vector2.zero;
        RemoveFromClassList(ActiveUssClass);
        _knob.style.translate = new Translate(0, 0);
        MoveBaseTo(RestPosition());
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (IsActive) return;

        _pointerId = evt.pointerId;
        this.CapturePointer(_pointerId);
        AddToClassList(ActiveUssClass);

        Vector2 local = evt.localPosition;
        MoveBaseTo(Floating ? ClampInside(local) : RestPosition());
        UpdateKnob(local);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != _pointerId) return;
        UpdateKnob(evt.localPosition);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != _pointerId) return;
        Release();
    }

    private void UpdateKnob(Vector2 local)
    {
        Vector2 offset = Vector2.ClampMagnitude(local - _center, Radius);
        _knob.style.translate = new Translate(offset.x, offset.y);

        Vector2 raw = offset / Radius;
        raw.y = -raw.y; // UI: y hacia abajo; salida: +y hacia arriba
        float magnitude = raw.magnitude;
        Value = magnitude < DeadZone
            ? Vector2.zero
            : raw.normalized * Mathf.InverseLerp(DeadZone, 1f, magnitude);
    }

    private void MoveBaseTo(Vector2 center)
    {
        _center = center;
        // La base se centra en el punto con translate -50% (ver USS)
        _base.style.left = center.x;
        _base.style.top = center.y;
    }

    private Vector2 RestPosition()
    {
        Rect r = contentRect;
        return new Vector2(r.width * RestAnchor.x, r.height * RestAnchor.y);
    }

    private Vector2 ClampInside(Vector2 p)
    {
        Rect r = contentRect;
        float margin = Radius * 0.9f;
        return new Vector2(
            Mathf.Clamp(p.x, margin, Mathf.Max(margin, r.width - margin)),
            Mathf.Clamp(p.y, margin, Mathf.Max(margin, r.height - margin)));
    }
}
