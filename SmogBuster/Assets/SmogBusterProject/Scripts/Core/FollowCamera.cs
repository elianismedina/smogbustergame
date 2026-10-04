using UnityEngine;

/// <summary>
/// Cámara del dron con dos modos:
/// - Primera persona: va en el morro del dron y mira hacia donde apunta. El modelo del dron se oculta
///   para no tapar la vista. Al estrellarse pasa a tercera persona para ver la caída.
/// - Tercera persona (persecución): se mantiene detrás del objetivo siguiendo su rumbo con suavizado.
///   Solo se recoloca detrás cuando el objetivo mira en la misma dirección que la cámara: si el dron
///   vuelve hacia la cámara o se mueve de lado, la cámara no gira, y así el joystick (relativo a la
///   cámara) no cambia de sentido a mitad de movimiento.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    public enum ViewMode
    {
        FirstPerson,
        ThirdPerson,
    }

    [SerializeField] private Transform _target;
    [SerializeField] private ViewMode _mode = ViewMode.FirstPerson;

    [Header("Primera persona")]
    [Tooltip("Posición de la cámara respecto al dron, en el marco de su rumbo (x lateral, y altura, z adelante).")]
    [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 0.25f, 0.9f);
    [Tooltip("Grados hacia abajo: deja ver el suelo y lo que hay delante.")]
    [SerializeField] private float _firstPersonPitch = 8f;
    [SerializeField] private float _firstPersonNearClip = 0.05f;
    [Tooltip("Modelo que se oculta en primera persona (los renderers de malla bajo él). Si se deja vacío, se usa el hijo Quadcopter_Drone.")]
    [SerializeField] private Transform _hiddenModel;

    [Header("Tercera persona")]
    [Tooltip("Posición relativa al objetivo, en el marco de su rumbo (x lateral, y altura, z atrás es negativo).")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 3f, -7f);
    [Tooltip("Punto al que mira, relativo al objetivo.")]
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float _positionSmoothing = 6f;
    [SerializeField] private float _yawSmoothing = 3f;
    [SerializeField] private float _minHeight = 0.5f;
    [Tooltip("La cámara solo se recoloca detrás si el objetivo mira a menos de estos grados de su frente.")]
    [SerializeField] private float _followConeAngle = 45f;

    private Camera _camera;
    private float _thirdPersonNearClip;
    private MeshRenderer[] _modelRenderers;
    private bool _modelHidden;
    private float _yaw;
    private float _kick;
    private float _shake;

    /// <summary>Golpe de cámara hacia arriba (grados) que vuelve solo, p. ej. al lanzar una semilla.</summary>
    public void Kick(float degrees) => _kick = Mathf.Max(_kick, degrees);

    /// <summary>Temblor continuo (grados) mientras se llame cada frame; se apaga solo si se deja de llamar.</summary>
    public void Shake(float degrees) => _shake = Mathf.Max(_shake, degrees);

    /// <summary>Rumbo de la cámara alrededor del objetivo (grados). Es estable aunque la cámara mire hacia el objetivo al moverse de lado.</summary>
    public float Heading => _yaw;

    /// <summary>Si es false, la cámara mantiene su rumbo actual (p. ej. mientras el dron cae girando).
    /// En primera persona, además, pasa a tercera persona para ver la caída.</summary>
    public bool FollowYaw { get; set; } = true;

    /// <summary>True mientras la vista es en primera persona (el dron gira con el stick lateral).</summary>
    public bool IsFirstPerson => _mode == ViewMode.FirstPerson && FollowYaw;

    private void Start()
    {
        if (_target == null)
        {
            Debug.LogWarning("FollowCamera: no hay objetivo asignado.", this);
            enabled = false;
            return;
        }

        _camera = GetComponent<Camera>();
        if (_camera != null) _thirdPersonNearClip = _camera.nearClipPlane;
        if (_hiddenModel == null) _hiddenModel = _target.Find("Quadcopter_Drone");
        if (_hiddenModel != null) _modelRenderers = _hiddenModel.GetComponentsInChildren<MeshRenderer>(true);

        _yaw = _target.eulerAngles.y;
        if (IsFirstPerson) PlaceFirstPerson();
        else
        {
            transform.position = DesiredPosition();
            transform.LookAt(_target.position + _lookOffset);
        }
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        bool firstPerson = IsFirstPerson;
        SetModelHidden(firstPerson);
        if (_camera != null) _camera.nearClipPlane = firstPerson ? _firstPersonNearClip : _thirdPersonNearClip;

        if (firstPerson)
        {
            PlaceFirstPerson();
            ApplyKickAndShake();
            return;
        }

        float dt = Time.deltaTime;
        if (FollowYaw)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(_yaw, _target.eulerAngles.y));
            // 1 si el objetivo mira hacia delante de la cámara; 0 a partir de _followConeAngle
            // (de lado o hacia la cámara), para que mover el stick de lado no haga girar la cámara
            float ahead = Mathf.Clamp01(1f - delta / _followConeAngle);
            float rate = _yawSmoothing * ahead;
            _yaw = Mathf.LerpAngle(_yaw, _target.eulerAngles.y, 1f - Mathf.Exp(-rate * dt));
        }

        Vector3 desired = DesiredPosition();
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_positionSmoothing * dt));
        transform.LookAt(_target.position + _lookOffset);
        ApplyKickAndShake();
    }

    // Se suma después de colocar la cámara, así no se acumula entre frames
    private void ApplyKickAndShake()
    {
        float dt = Time.deltaTime;
        if (_kick <= 0.001f && _shake <= 0.001f) return;

        float t = Time.time * 40f;
        float shakeX = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * _shake;
        float shakeY = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * _shake;
        transform.rotation *= Quaternion.Euler(-_kick + shakeX, shakeY, 0f);

        _kick = Mathf.MoveTowards(_kick, 0f, (_kick * 8f + 2f) * dt);
        _shake = Mathf.MoveTowards(_shake, 0f, 20f * dt);
    }

    // En el morro del dron, siguiendo su rumbo pero no su inclinación (evita mareos al acelerar)
    private void PlaceFirstPerson()
    {
        _yaw = _target.eulerAngles.y;
        Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
        transform.position = _target.position + heading * _firstPersonOffset;
        transform.rotation = heading * Quaternion.Euler(_firstPersonPitch, 0f, 0f);
    }

    private void SetModelHidden(bool hidden)
    {
        if (_modelRenderers == null || hidden == _modelHidden) return;
        _modelHidden = hidden;
        foreach (MeshRenderer r in _modelRenderers)
        {
            if (r != null) r.enabled = !hidden;
        }
    }

    private Vector3 DesiredPosition()
    {
        Vector3 position = _target.position + Quaternion.Euler(0f, _yaw, 0f) * _offset;
        position.y = Mathf.Max(position.y, _minHeight);
        return position;
    }
}
