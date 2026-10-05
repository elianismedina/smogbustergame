using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Cámara del dron, va en la Main Camera junto al CinemachineBrain. No mueve la cámara: elige qué
/// cámara virtual manda subiendo su prioridad, y traduce los efectos del juego a Cinemachine.
/// - Tercera persona cercana (por defecto, CM_TerceraPersona): detrás y por encima del dron, mirando por
///   encima y por delante de él, así el dron se ve abajo en el centro y la mira queda cerca del centro.
/// - Primera persona (CM_PrimeraPersona): fija en FPV_Mount, en el centro del dron un poco por detrás de
///   las hélices delanteras. Se oculta el cuerpo; las hélices y motores delanteros siguen a la vista.
///   Se elige en juego con el botón de cámara del HUD (<see cref="ToggleView"/>).
/// - Caída (CM_Caida): al estrellarse, la cámara se queda donde estaba y mira cómo cae el dron.
/// En los dos modos de vuelo el stick lateral gira el dron (<see cref="SteersWithHeading"/>): si el stick
/// moviera de lado respecto a una cámara que sigue al dron, el dron acabaría dando vueltas.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    public enum ViewMode
    {
        FirstPerson,
        ThirdPerson,
    }

    [SerializeField] private Transform _target;
    [SerializeField] private ViewMode _mode = ViewMode.ThirdPerson;

    [Header("Cámaras virtuales")]
    [SerializeField] private CinemachineCamera _thirdPersonCamera;
    [SerializeField] private CinemachineCamera _firstPersonCamera;
    [SerializeField] private CinemachineCamera _crashCamera;
    [Tooltip("Prioridad que toma la cámara elegida (primera persona o caída) para mandar sobre la de tercera persona.")]
    [SerializeField] private int _activePriority = 30;

    [Header("Primera persona")]
    [Tooltip("Punto de montaje de la cámara, hijo del dron. Solo sigue el rumbo: el dron no se inclina en la raíz.")]
    [SerializeField] private Transform _fpvMount;
    [Tooltip("Posición del montaje respecto al dron (x lateral, y altura, z adelante).")]
    [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 0.3f, -0.8f);
    [Tooltip("Grados hacia abajo: deja ver el suelo y lo que hay delante.")]
    [SerializeField] private float _firstPersonPitch = 8f;
    [Tooltip("Modelo que se oculta en primera persona (los renderers de malla bajo él). Si se deja vacío, se usa el hijo Quadcopter_Drone.")]
    [SerializeField] private Transform _hiddenModel;
    [Tooltip("Dejar a la vista las hélices y motores que quedan delante de la cámara.")]
    [SerializeField] private bool _showFrontPropellers = true;
    [Tooltip("A esta distancia del montaje (metros) se considera que la cámara ya llegó a primera persona.")]
    [SerializeField] private float _arrivedDistance = 0.5f;

    [Header("Efectos")]
    [Tooltip("Fuente de impulsos en el dron (golpes de cámara y choque).")]
    [SerializeField] private CinemachineImpulseSource _impulse;
    [Tooltip("Metros de golpe por cada grado de Kick (el impulso desplaza la cámara, no la gira).")]
    [SerializeField] private float _kickPerDegree = 0.12f;
    [SerializeField] private float _crashImpulse = 0.8f;
    [Tooltip("Rapidez con la que se apaga el temblor si se deja de llamar a Shake (grados por segundo).")]
    [SerializeField] private float _shakeDecay = 20f;

    private CinemachineBasicMultiChannelPerlin[] _noises;
    private MeshRenderer[] _modelRenderers;
    private bool _modelHidden;
    private bool _crashed;
    private float _shake;

    /// <summary>Golpe de cámara hacia arriba (grados) que vuelve solo, p. ej. al lanzar una semilla.</summary>
    public void Kick(float degrees)
    {
        if (_impulse != null && degrees > 0f) _impulse.GenerateImpulseWithVelocity(Vector3.up * degrees * _kickPerDegree);
    }

    /// <summary>Temblor continuo (grados) mientras se llame cada frame; se apaga solo si se deja de llamar.</summary>
    public void Shake(float degrees) => _shake = Mathf.Max(_shake, degrees);

    /// <summary>Rumbo real de la cámara (grados), el que da el CinemachineBrain.</summary>
    public float Heading => transform.eulerAngles.y;

    /// <summary>Si es false, la cámara deja de seguir al dron (p. ej. mientras cae girando):
    /// pasa a la cámara de caída, que se queda quieta y lo mira.</summary>
    public bool FollowYaw { get; set; } = true;

    /// <summary>True mientras la vista es en primera persona (la cámara ya llegó al dron, no durante la mezcla).</summary>
    public bool IsFirstPerson { get; private set; }

    /// <summary>Vista elegida (la de primera persona no se usa mientras el dron cae).</summary>
    public ViewMode Mode => _mode;

    /// <summary>Cambia de vista en juego, con la mezcla del brain. No hace nada mientras el dron cae.</summary>
    public void SetMode(ViewMode mode)
    {
        if (!FollowYaw) return;
        _mode = mode;
        UpdatePriorities();
    }

    /// <summary>Pasa de tercera a primera persona o al revés.</summary>
    public void ToggleView() => SetMode(_mode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson);

    private bool WantsFirstPerson => _mode == ViewMode.FirstPerson && FollowYaw;

    /// <summary>True mientras la cámara sigue el rumbo del dron (los dos modos, salvo al caer):
    /// el stick lateral gira el dron y el vertical lo mueve adelante y atrás.</summary>
    public bool SteersWithHeading => FollowYaw;

    private void Start()
    {
        if (_target == null)
        {
            Debug.LogWarning("FollowCamera: no hay objetivo asignado.", this);
            enabled = false;
            return;
        }

        if (_fpvMount != null) _fpvMount.SetLocalPositionAndRotation(_firstPersonOffset, Quaternion.Euler(_firstPersonPitch, 0f, 0f));
        if (_hiddenModel == null) _hiddenModel = _target.Find("Quadcopter_Drone");
        if (_hiddenModel != null) _modelRenderers = HideableRenderers(_hiddenModel);
        _noises = new[]
        {
            _thirdPersonCamera != null ? _thirdPersonCamera.GetComponent<CinemachineBasicMultiChannelPerlin>() : null,
            _firstPersonCamera != null ? _firstPersonCamera.GetComponent<CinemachineBasicMultiChannelPerlin>() : null,
        };
        UpdatePriorities();
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        if (!FollowYaw && !_crashed) StartCrash();
        else if (FollowYaw) _crashed = false;

        UpdatePriorities();
        // El cuerpo se oculta y la mira sale de la cámara solo al terminar la mezcla, cuando la cámara ya
        // está en el dron; si no, el dron desaparecería a mitad de camino
        IsFirstPerson = WantsFirstPerson
                        && (_fpvMount == null || (transform.position - _fpvMount.position).sqrMagnitude < _arrivedDistance * _arrivedDistance);
        SetModelHidden(IsFirstPerson);

        // El perfil de ruido gira la cámara 1 grado por unidad de ganancia
        foreach (CinemachineBasicMultiChannelPerlin noise in _noises)
        {
            if (noise != null) noise.AmplitudeGain = _shake;
        }
        _shake = Mathf.MoveTowards(_shake, 0f, _shakeDecay * Time.deltaTime);
    }

    private void UpdatePriorities()
    {
        if (_firstPersonCamera != null) _firstPersonCamera.Priority = WantsFirstPerson ? _activePriority : 10;
        if (_crashCamera != null) _crashCamera.Priority = _crashed ? _activePriority + 10 : 0;
    }

    // La cámara de caída arranca justo donde está la cámara (el brain hace corte, no mezcla)
    private void StartCrash()
    {
        _crashed = true;
        if (_crashCamera != null) _crashCamera.ForceCameraPosition(transform.position, transform.rotation);
        if (_impulse != null) _impulse.GenerateImpulseWithVelocity(Random.onUnitSphere * _crashImpulse);
    }

    // Todo el modelo salvo, si se pide, las hélices y motores que quedan por delante de la cámara
    // (los brazos salen del centro, justo donde está la cámara, y taparían la vista).
    // Se decide por la posición, no por el nombre: en el modelo las piezas "B" son las del morro.
    private MeshRenderer[] HideableRenderers(Transform model)
    {
        var hideable = new System.Collections.Generic.List<MeshRenderer>();
        foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name;
            bool frontPart = _showFrontPropellers
                             && (n.StartsWith("Prop") || n.StartsWith("Motor"))
                             && _target.InverseTransformPoint(r.bounds.center).z > _firstPersonOffset.z + 0.2f;
            if (!frontPart) hideable.Add(r);
        }
        return hideable.ToArray();
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
}
