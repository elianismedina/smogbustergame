using UnityEngine;

/// <summary>
/// Controlador de movimiento arcade del quadcopter.
/// - Velocidad horizontal: PID de velocidad -> aceleración.
/// - Altitud: PID de altura con setpoint que sube/baja con la entrada.
/// - Yaw: giro suavizado sobre el eje Y.
/// Usa ForceMode.Acceleration, así que la masa del Rigidbody no afecta a la sensación.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(QuadInput))]
public class QuadcopterController : MonoBehaviour
{
    [Header("Movimiento horizontal")]
    [SerializeField] private float _maxSpeed = 12f;
    [SerializeField] private float _maxAcceleration = 25f;
    [Tooltip("Qué tan rápido cambia la velocidad objetivo (m/s²). Suaviza el stick táctil.")]
    [SerializeField] private float _targetVelocityRate = 40f;
    [SerializeField] private PIDController _velocityPid = new PIDController(4f, 0f, 0.2f, 5f);

    [Header("Altitud")]
    [SerializeField] private float _climbSpeed = 5f;
    [SerializeField] private float _minAltitude = 0.5f;
    [SerializeField] private float _maxAltitude = 60f;
    [SerializeField] private float _maxVerticalAcceleration = 20f;
    [SerializeField] private PIDController _altitudePid = new PIDController(6f, 0.5f, 4f, 5f);
    [Tooltip("Máximo que la altitud objetivo puede quedar por debajo del dron (m). Evita que, posado en una azotea, el objetivo siga bajando y tarde en despegar.")]
    [SerializeField] private float _maxAltitudeBelow = 1f;

    [Header("Giro (yaw)")]
    [SerializeField] private float _maxYawRate = 120f;
    [SerializeField] private float _yawSmoothing = 8f;

    [Header("Referencia de movimiento")]
    [Tooltip("Opcional. Si se asigna (p. ej. la cámara), el stick es relativo a su orientación; si no, al rumbo del dron.")]
    [SerializeField] private Transform _moveReference;

    private Rigidbody _rb;
    private QuadInput _input;

    private Vector3 _targetVelocity;
    private float _targetAltitude;
    private float _heading;
    private float _yawRate;

    private PIDController _velocityPidX;
    private PIDController _velocityPidZ;

    /// <summary>Velocidad horizontal actual (mundo).</summary>
    public Vector3 HorizontalVelocity => new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

    public float MaxSpeed => _maxSpeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _input = GetComponent<QuadInput>();

        // La gravedad está desactivada y la rotación congelada en el prefab: el control es por velocidad.
        _rb.useGravity = false;

        // Un PID por eje horizontal con las mismas ganancias (cada uno guarda su propio estado).
        _velocityPidX = new PIDController(_velocityPid.Kp, _velocityPid.Ki, _velocityPid.Kd, _velocityPid.IntegralLimit);
        _velocityPidZ = new PIDController(_velocityPid.Kp, _velocityPid.Ki, _velocityPid.Kd, _velocityPid.IntegralLimit);
    }

    private void OnEnable()
    {
        _targetAltitude = _rb.position.y;
        _heading = _rb.rotation.eulerAngles.y;
        _targetVelocity = Vector3.zero;
        _yawRate = 0f;
        _velocityPidX.Reset();
        _velocityPidZ.Reset();
        _altitudePid.Reset();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        UpdateYaw(dt);
        UpdateHorizontal(dt);
        UpdateAltitude(dt);
    }

    private void UpdateYaw(float dt)
    {
        float targetRate = _input.Yaw * _maxYawRate;
        _yawRate = Mathf.Lerp(_yawRate, targetRate, 1f - Mathf.Exp(-_yawSmoothing * dt));
        _heading += _yawRate * dt;
        _rb.MoveRotation(Quaternion.Euler(0f, _heading, 0f));
    }

    private void UpdateHorizontal(float dt)
    {
        // Referencia de orientación proyectada al plano horizontal.
        float referenceYaw = _moveReference != null ? _moveReference.eulerAngles.y : _heading;
        Quaternion yawRotation = Quaternion.Euler(0f, referenceYaw, 0f);

        Vector2 stick = _input.Move;
        Vector3 desired = yawRotation * new Vector3(stick.x, 0f, stick.y) * _maxSpeed;

        // La velocidad objetivo se acerca a la deseada con un límite de cambio (suaviza el input táctil).
        _targetVelocity = Vector3.MoveTowards(_targetVelocity, desired, _targetVelocityRate * dt);

        Vector3 velocity = HorizontalVelocity;
        Vector3 acceleration = new Vector3(
            _velocityPidX.Update(_targetVelocity.x - velocity.x, dt),
            0f,
            _velocityPidZ.Update(_targetVelocity.z - velocity.z, dt));

        acceleration = Vector3.ClampMagnitude(acceleration, _maxAcceleration);
        _rb.AddForce(acceleration, ForceMode.Acceleration);
    }

    private void UpdateAltitude(float dt)
    {
        _targetAltitude = Mathf.Clamp(
            _targetAltitude + _input.Climb * _climbSpeed * dt,
            _minAltitude,
            _maxAltitude);
        _targetAltitude = Mathf.Max(_targetAltitude, _rb.position.y - _maxAltitudeBelow);

        float error = _targetAltitude - _rb.position.y;
        float acceleration = Mathf.Clamp(_altitudePid.Update(error, dt), -_maxVerticalAcceleration, _maxVerticalAcceleration);
        _rb.AddForce(Vector3.up * acceleration, ForceMode.Acceleration);
    }
}
