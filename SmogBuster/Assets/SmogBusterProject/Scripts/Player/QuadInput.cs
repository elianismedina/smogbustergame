using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada del quadcopter (GDD 6.2). Combina teclado, ratón y gamepad con los valores de los
/// controles táctiles del HUD (métodos Set*). No hay giro manual: el dron gira hacia donde se mueve.
/// Teclado: WASD mover, Espacio subir, Shift izquierdo bajar, clic izquierdo Rayo.
/// </summary>
public class QuadInput : MonoBehaviour
{
    private InputAction _move;
    private InputAction _climb;
    private InputAction _beam;

    private Vector2 _externalMove;
    private float _externalClimb;
    private bool _externalBeam;

    /// <summary>Movimiento horizontal: x = lateral, y = adelante/atrás.</summary>
    public Vector2 Move { get; private set; }

    /// <summary>Ascenso (+1) / descenso (-1).</summary>
    public float Climb { get; private set; }

    /// <summary>Rayo Purificador pulsado (se mantiene para disparar).</summary>
    public bool Beam { get; private set; }

    /// <summary>Con true se ignora toda entrada (p. ej. al terminar la partida).</summary>
    public bool Locked { get; set; }

    // Los sticks en pantalla llaman a estos métodos.
    public void SetMove(Vector2 value) => _externalMove = value;
    public void SetClimb(float value) => _externalClimb = value;
    public void SetBeam(bool value) => _externalBeam = value;

    private void Awake()
    {
        _move = new InputAction("Move", InputActionType.Value);
        _move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone");

        _climb = new InputAction("Climb", InputActionType.Value);
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/space")
            .With("Negative", "<Keyboard>/leftShift");
        _climb.AddBinding("<Gamepad>/rightStick/y").WithProcessor("axisDeadzone");

        _beam = new InputAction("Beam", InputActionType.Button);
        _beam.AddBinding("<Mouse>/leftButton");
        _beam.AddBinding("<Gamepad>/rightTrigger");
    }

    private void OnEnable()
    {
        _move.Enable();
        _climb.Enable();
        _beam.Enable();
    }

    private void OnDisable()
    {
        _move.Disable();
        _climb.Disable();
        _beam.Disable();
    }

    private void OnDestroy()
    {
        _move.Dispose();
        _climb.Dispose();
        _beam.Dispose();
    }

    // Se lee una vez por frame; FixedUpdate solo consume los valores en caché.
    private void Update()
    {
        if (Locked)
        {
            Move = Vector2.zero;
            Climb = 0f;
            Beam = false;
            return;
        }

        Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>() + _externalMove, 1f);
        Climb = Mathf.Clamp(_climb.ReadValue<float>() + _externalClimb, -1f, 1f);
        Beam = _beam.IsPressed() || _externalBeam;
    }
}
