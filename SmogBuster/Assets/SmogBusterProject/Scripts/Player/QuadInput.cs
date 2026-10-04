using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada del quadcopter (GDD 6.2). Combina teclado, ratón y gamepad con los valores de los
/// controles táctiles del HUD (métodos Set*). En primera persona el stick lateral gira el dron; en tercera, gira hacia donde se mueve.
/// Teclado: WASD mover, Espacio subir, Shift izquierdo bajar, clic izquierdo Rayo, clic derecho Semilla.
/// Mando (Bluetooth o USB, GDD 10.4): stick izquierdo mover; stick derecho, cruceta o RB/LB subir y bajar;
/// RT o A Rayo; LT o X Semilla; Start pausa (ver PauseMenuController).
/// </summary>
public class QuadInput : MonoBehaviour
{
    private InputAction _move;
    private InputAction _climb;
    private InputAction _beam;
    private InputAction _seed;

    private Vector2 _externalMove;
    private float _externalClimb;
    private bool _externalBeam;
    private bool _externalSeed;

    /// <summary>Movimiento horizontal: x = lateral, y = adelante/atrás.</summary>
    public Vector2 Move { get; private set; }

    /// <summary>Ascenso (+1) / descenso (-1).</summary>
    public float Climb { get; private set; }

    /// <summary>Rayo Purificador pulsado (se mantiene para disparar).</summary>
    public bool Beam { get; private set; }

    /// <summary>Botón de Semilla pulsado (se lanza una semilla al pulsar).</summary>
    public bool Seed { get; private set; }

    /// <summary>Con true se ignora toda entrada (p. ej. al terminar la partida).</summary>
    public bool Locked { get; set; }

    // Los sticks en pantalla llaman a estos métodos.
    public void SetMove(Vector2 value) => _externalMove = value;
    public void SetClimb(float value) => _externalClimb = value;
    public void SetBeam(bool value) => _externalBeam = value;
    public void SetSeed(bool value) => _externalSeed = value;

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
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Gamepad>/dpad/up")
            .With("Negative", "<Gamepad>/dpad/down");
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Gamepad>/rightShoulder")
            .With("Negative", "<Gamepad>/leftShoulder");

        _beam = new InputAction("Beam", InputActionType.Button);
        _beam.AddBinding("<Mouse>/leftButton");
        _beam.AddBinding("<Gamepad>/rightTrigger");
        _beam.AddBinding("<Gamepad>/buttonSouth");

        _seed = new InputAction("Seed", InputActionType.Button);
        _seed.AddBinding("<Mouse>/rightButton");
        _seed.AddBinding("<Gamepad>/leftTrigger");
        _seed.AddBinding("<Gamepad>/buttonWest");
    }

    private void OnEnable()
    {
        _move.Enable();
        _climb.Enable();
        _beam.Enable();
        _seed.Enable();
    }

    private void OnDisable()
    {
        _move.Disable();
        _climb.Disable();
        _beam.Disable();
        _seed.Disable();
    }

    private void OnDestroy()
    {
        _move.Dispose();
        _climb.Dispose();
        _beam.Dispose();
        _seed.Dispose();
    }

    // Se lee una vez por frame; FixedUpdate solo consume los valores en caché.
    private void Update()
    {
        if (Locked)
        {
            Move = Vector2.zero;
            Climb = 0f;
            Beam = false;
            Seed = false;
            return;
        }

        Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>() + _externalMove, 1f);
        Climb = Mathf.Clamp(_climb.ReadValue<float>() + _externalClimb, -1f, 1f);
        Beam = _beam.IsPressed() || _externalBeam;
        Seed = _seed.IsPressed() || _externalSeed;
    }
}
