using UnityEngine;

/// <summary>
/// Inclina el modelo visual según la velocidad local (el Rigidbody raíz tiene la rotación congelada)
/// y hace girar las hélices si se encuentran hijos cuyo nombre contenga prop/rotor/blade/helice.
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
    [SerializeField] private float _propellerSpeed = 2400f;

    private Quaternion _baseRotation;
    private Vector3 _basePosition;
    private Quaternion _tilt;

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

        float spin = _propellerSpeed * Time.deltaTime;
        for (int i = 0; i < _propellers.Length; i++)
        {
            // Sentidos alternados como en un quadcopter real.
            _propellers[i].Rotate(0f, (i % 2 == 0 ? spin : -spin), 0f, Space.Self);
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
