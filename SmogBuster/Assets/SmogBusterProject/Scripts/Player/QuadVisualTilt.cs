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
    [Tooltip("Grados por segundo con el dron quieto en el aire (hovering).")]
    [SerializeField] private float _propellerSpeed = 2700f;
    [Tooltip("Grados por segundo a máxima velocidad o subiendo.")]
    [SerializeField] private float _propellerMaxSpeed = 3600f;
    [SerializeField] private float _propellerSmoothing = 4f;
    [Tooltip("Giro máximo de las palas por frame. Por encima de unos 45° el ojo ve las palas quietas o girando al revés " +
             "(efecto estroboscópico); el resto de la velocidad lo transmite el disco de giro.")]
    [SerializeField] private float _maxStepPerFrame = 40f;
    [Tooltip("Material semitransparente del disco que se ve con las hélices a toda velocidad (opcional).")]
    [SerializeField] private Material _blurDiscMaterial;
    [Tooltip("Opacidad del disco en hovering y a máxima velocidad.")]
    [SerializeField] private Vector2 _blurDiscAlpha = new Vector2(0.25f, 0.4f);

    private Quaternion _baseRotation;
    private Vector3 _basePosition;
    private Quaternion _tilt;
    private float _spinRate;
    private Rigidbody _body;
    private float[] _spinSigns;
    private Renderer[] _blurDiscs;
    private MaterialPropertyBlock _discBlock;

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

        if (_blurDiscMaterial != null) CreateBlurDiscs();
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
        // Tiempo sin escalar: los motores no se paran en hovering aunque el juego esté en pausa o en el tutorial
        float dt = Time.unscaledDeltaTime;
        _spinRate = Mathf.Lerp(_spinRate <= 0f ? targetRate : _spinRate, targetRate, 1f - Mathf.Exp(-_propellerSmoothing * dt));
        UpdateBlurDiscs(Mathf.InverseLerp(_propellerSpeed, _propellerMaxSpeed, _spinRate));

        // El eje es el "arriba" del dron: el eje local de las hélices importadas de Blender apunta de lado,
        // y girar sobre él las hacía voltear en vertical. El paso por frame se limita para que no parezcan paradas.
        float spin = Mathf.Min(_spinRate * dt, _maxStepPerFrame);
        Vector3 axis = _model.up;
        for (int i = 0; i < _propellers.Length; i++)
        {
            _propellers[i].Rotate(axis, spin * _spinSigns[i], Space.World);
        }
    }

    // Un disco fino y semitransparente bajo cada hélice: es como se ve una hélice real a muchas revoluciones.
    // Va colgado del modelo (no de la hélice) para que no gire con ella ni herede su escala de Blender.
    private void CreateBlurDiscs()
    {
        _discBlock = new MaterialPropertyBlock();
        _blurDiscs = new Renderer[_propellers.Length];
        for (int i = 0; i < _propellers.Length; i++)
        {
            Renderer prop = _propellers[i].GetComponent<Renderer>();
            float diameter = prop != null ? Mathf.Max(prop.bounds.size.x, prop.bounds.size.z) : 1f;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = _propellers[i].name + "_Blur";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(_model, false);
            disc.transform.position = _propellers[i].position;
            disc.transform.localRotation = Quaternion.identity;
            disc.transform.localScale = new Vector3(diameter, 0.004f, diameter);

            Renderer r = disc.GetComponent<Renderer>();
            r.sharedMaterial = _blurDiscMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _blurDiscs[i] = r;
        }
    }

    private void UpdateBlurDiscs(float speed01)
    {
        if (_blurDiscs == null) return;
        Color color = _blurDiscMaterial.color;
        color.a = Mathf.Lerp(_blurDiscAlpha.x, _blurDiscAlpha.y, speed01);
        foreach (Renderer disc in _blurDiscs)
        {
            disc.GetPropertyBlock(_discBlock);
            _discBlock.SetColor("_BaseColor", color);
            disc.SetPropertyBlock(_discBlock);
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
