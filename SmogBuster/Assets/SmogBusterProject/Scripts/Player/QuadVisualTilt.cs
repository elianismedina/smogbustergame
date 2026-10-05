using UnityEngine;

/// <summary>
/// Inclina el modelo visual según la velocidad local (el Rigidbody raíz tiene la rotación congelada)
/// y hace girar las hélices (hijos cuyo nombre contenga prop/rotor/blade/helice) sobre el eje vertical del dron,
/// más rápido al acelerar o subir y con las diagonales en el mismo sentido, como un quadcopter real.
/// Otros scripts (p. ej. <see cref="DroneAnimations"/>) pueden sumar un giro y un desplazamiento extra al modelo.
/// </summary>
public class QuadVisualTilt : MonoBehaviour
{
    [SerializeField] private QuadcopterController _controller;
    [Tooltip("Hijo con el modelo del dron. Si se deja vacío se usa este mismo transform.")]
    [SerializeField] private Transform _model;
    [SerializeField] private float _maxTiltAngle = 25f;
    [SerializeField] private float _tiltSmoothing = 6f;

    [Header("Hélices")]
    [SerializeField] private Transform[] _propellers;
    [Tooltip("Grados por segundo con el dron quieto en el aire.")]
    [SerializeField] private float _propellerSpeed = 1500f;
    [Tooltip("Grados por segundo a máxima velocidad o subiendo.")]
    [SerializeField] private float _propellerMaxSpeed = 2100f;
    [SerializeField] private float _propellerSmoothing = 4f;

    private Quaternion _baseRotation;
    private Vector3 _basePosition;
    private Quaternion _tilt;
    private float _spinRate;
    private Rigidbody _body;
    private float[] _spinSigns;

    /// <summary>Giro que se suma a la inclinación (retroceso, celebración...). Se aplica en el espacio del modelo.</summary>
    public Quaternion ExtraRotation { get; set; } = Quaternion.identity;

    /// <summary>Desplazamiento local que se suma a la posición del modelo.</summary>
    public Vector3 ExtraOffset { get; set; }

    private void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponentInParent<QuadcopterController>();
        }

        if (_model == null)
        {
            _model = transform;
        }

        _baseRotation = _model.localRotation;
        _basePosition = _model.localPosition;
        _tilt = _baseRotation;

        if (_propellers == null || _propellers.Length == 0)
        {
            _propellers = FindPropellers(_model);
        }

        _body = _controller != null ? _controller.GetComponent<Rigidbody>() : null;
        // Como en un quadcopter real, las hélices en diagonal giran en el mismo sentido
        _spinSigns = new float[_propellers.Length];
        for (int i = 0; i < _propellers.Length; i++)
        {
            Vector3 p = _model.InverseTransformPoint(_propellers[i].position);
            _spinSigns[i] = p.x * p.z >= 0f ? 1f : -1f;
        }
    }

    private void Update()
    {
        if (_controller == null)
        {
            return;
        }

        Vector3 localVelocity = _controller.transform.InverseTransformDirection(_controller.HorizontalVelocity);
        float speedFactor = _controller.MaxSpeed > 0f ? 1f / _controller.MaxSpeed : 0f;

        // Adelante -> cabeceo hacia abajo (+X); derecha -> alabeo hacia la derecha (-Z).
        float pitch = Mathf.Clamp(localVelocity.z * speedFactor, -1f, 1f) * _maxTiltAngle;
        float roll = -Mathf.Clamp(localVelocity.x * speedFactor, -1f, 1f) * _maxTiltAngle;

        Quaternion target = _baseRotation * Quaternion.Euler(pitch, 0f, roll);
        _tilt = Quaternion.Slerp(_tilt, target, 1f - Mathf.Exp(-_tiltSmoothing * Time.deltaTime));
        _model.localRotation = _tilt * ExtraRotation;
        _model.localPosition = _basePosition + ExtraOffset;

        // Más revoluciones al acelerar o subir, como un dron real
        float effort = Mathf.Clamp01(Mathf.Max(Mathf.Abs(localVelocity.z), Mathf.Abs(localVelocity.x)) * speedFactor
                                     + (_body != null ? Mathf.Max(0f, _body.linearVelocity.y) * 0.15f : 0f));
        float targetRate = Mathf.Lerp(_propellerSpeed, _propellerMaxSpeed, effort);
        _spinRate = Mathf.Lerp(_spinRate <= 0f ? targetRate : _spinRate, targetRate, 1f - Mathf.Exp(-_propellerSmoothing * Time.deltaTime));

        // El eje es el "arriba" del dron: el eje local de las hélices importadas de Blender apunta de lado,
        // y girar sobre él las hacía voltear en vertical
        float spin = _spinRate * Time.deltaTime;
        Vector3 axis = _model.up;
        for (int i = 0; i < _propellers.Length; i++)
        {
            _propellers[i].Rotate(axis, spin * _spinSigns[i], Space.World);
        }
    }

    private static Transform[] FindPropellers(Transform root)
    {
        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("prop") || n.Contains("rotor") || n.Contains("blade") || n.Contains("helice"))
            {
                found.Add(t);
            }
        }

        return found.ToArray();
    }
}
