using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada del quadcopter. Combina teclado/gamepad (para pruebas en editor) con
/// valores externos (sticks en pantalla en móvil) mediante los métodos Set*.
/// </summary>
public class QuadInput : MonoBehaviour
{
    private InputAction _move;
    private InputAction _climb;
    private InputAction _yaw;

    private Vector2 _externalMove;
    private float _externalClimb;
    private float _externalYaw;

    /// <summary>Movimiento horizontal: x = lateral, y = adelante/atrás.</summary>
    public Vector2 Move { get; private set; }

    /// <summary>Ascenso (+1) / descenso (-1).</summary>
    public float Climb { get; private set; }

    /// <summary>Giro (yaw): +1 derecha, -1 izquierda.</summary>
    public float Yaw { get; private set; }

    // Los sticks en pantalla llaman a estos métodos.
    public void SetMove(Vector2 value) => _externalMove = value;
    public void SetClimb(float value) => _externalClimb = value;
    public void SetYaw(float value) => _externalYaw = value;

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
            .With("Negative", "<Keyboard>/leftCtrl");
        _climb.AddBinding("<Gamepad>/rightStick/y").WithProcessor("axisDeadzone");

        _yaw = new InputAction("Yaw", InputActionType.Value);
        _yaw.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/e")
            .With("Negative", "<Keyboard>/q");
        _yaw.AddBinding("<Gamepad>/rightStick/x").WithProcessor("axisDeadzone");
    }

    private void OnEnable()
    {
        _move.Enable();
        _climb.Enable();
        _yaw.Enable();
    }

    private void OnDisable()
    {
        _move.Disable();
        _climb.Disable();
        _yaw.Disable();
    }

    private void OnDestroy()
    {
        _move.Dispose();
        _climb.Dispose();
        _yaw.Dispose();
    }

    // Se lee una vez por frame; FixedUpdate solo consume los valores en caché.
    private void Update()
    {
        Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>() + _externalMove, 1f);
        Climb = Mathf.Clamp(_climb.ReadValue<float>() + _externalClimb, -1f, 1f);
        Yaw = Mathf.Clamp(_yaw.ReadValue<float>() + _externalYaw, -1f, 1f);
    }
}
