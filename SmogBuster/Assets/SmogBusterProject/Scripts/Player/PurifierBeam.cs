using UnityEngine;

/// <summary>
/// Rayo Purificador (GDD 6.1): mientras se mantiene pulsado, lanza un haz desde el cañón azul.
/// La primera nube de smog que toca se disipa al instante; un edificio corta el haz.
/// Apuntado:
/// - La mira del HUD marca hacia dónde va el rayo: en primera persona, el centro de la cámara.
/// - Fijado automático: si ninguna nube está justo en la mira, el rayo se fija en la nube visible más
///   cercana a la mira dentro de un cono (20° a los lados, 40° arriba/abajo), aunque esté más alta o más baja.
/// Aunque no se dispare, marca la nube fijada (<see cref="CurrentTarget"/>) para que el jugador sepa
/// que, si dispara ahora, acierta.
/// </summary>
[RequireComponent(typeof(QuadInput))]
public class PurifierBeam : MonoBehaviour
{
    [Tooltip("Punta del cañón azul. Si se deja vacío, se busca un hijo llamado Cannon_Beam.")]
    [SerializeField] private Transform _muzzle;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _range = 35f;
    [Tooltip("Radio del barrido a lo largo de la mira: tolerancia al apuntar.")]
    [SerializeField] private float _aimRadius = 1.5f;
    [Tooltip("Fijado automático: grados a izquierda/derecha de la mira dentro de los que se fija una nube.")]
    [SerializeField] private float _assistAngle = 20f;
    [Tooltip("Fijado automático: grados arriba/abajo. Más amplio que el horizontal porque la altura es lo difícil de igualar.")]
    [SerializeField] private float _assistVerticalAngle = 40f;
    [Tooltip("En tercera persona: grados hacia abajo respecto al frente del dron.")]
    [SerializeField] private float _downAngle = 4f;
    [Tooltip("En primera persona el haz se dibuja desde aquí (m por delante del cañón) para no tapar la vista.")]
    [SerializeField] private float _firstPersonLineStart = 2.5f;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private QuadInput _input;
    private QuadcopterCrash _crash;
    private SeedLauncher _seeds;
    private FollowCamera _camera;

    /// <summary>True mientras el haz está activo (para sonido y efectos).</summary>
    public bool IsFiring { get; private set; }

    /// <summary>Nube que acertaría el Rayo si se disparase ahora (null si ninguna).</summary>
    public SmogCloud CurrentTarget { get; private set; }

    /// <summary>Origen y dirección de la mira (para dibujarla en el HUD).</summary>
    public Vector3 AimOrigin { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public float Range => _range;

    private void Awake()
    {
        _input = GetComponent<QuadInput>();
        _crash = GetComponent<QuadcopterCrash>();
        _seeds = GetComponent<SeedLauncher>();
        if (Camera.main != null) _camera = Camera.main.GetComponent<FollowCamera>();
        if (_muzzle == null) _muzzle = FindChild(transform, "Cannon_Beam");
        if (_line != null) _line.enabled = false;
    }

    private void Update()
    {
        bool crashed = _crash != null && _crash.HasCrashed;
        bool over = GameSession.Instance != null && GameSession.Instance.IsOver;
        bool active = !crashed && !over && Time.timeScale > 0f;
        // No se usa a la vez que la semilla (GDD 7.5)
        bool seedBusy = _seeds != null && _seeds.IsBusy;
        IsFiring = _input.Beam && active && !seedBusy;
        if (_line != null) _line.enabled = IsFiring;

        Vector3 muzzle = _muzzle != null ? _muzzle.position : transform.position;
        bool firstPerson = _camera != null && _camera.IsFirstPerson;

        // La mira: en primera persona, el centro de la cámara; si no, el frente del dron (sin su inclinación)
        if (firstPerson)
        {
            AimOrigin = _camera.transform.position;
            AimDirection = _camera.transform.forward;
        }
        else
        {
            Vector3 forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            AimOrigin = muzzle;
            AimDirection = Quaternion.AngleAxis(_downAngle, Vector3.Cross(Vector3.up, forward)) * forward;
        }

        float end = Cast(AimOrigin, AimDirection, out SmogCloud target);
        if (target == null) target = FindAssistTarget(AimOrigin, AimDirection);
        SetTarget(active ? target : null);
        if (!IsFiring) return;

        // El haz va a la nube fijada; si no hay, sigue la mira hasta donde choque
        Vector3 beamEnd = target != null ? target.transform.position : AimOrigin + AimDirection * end;
        if (target != null)
        {
            SetTarget(null);
            target.Purify();
        }

        if (_line != null)
        {
            Vector3 toEnd = beamEnd - muzzle;
            // En primera persona la cámara está junto al cañón: empezar el haz algo más adelante
            float lineStart = firstPerson ? Mathf.Min(_firstPersonLineStart, toEnd.magnitude * 0.5f) : 0f;
            _line.SetPosition(0, muzzle + toEnd.normalized * lineStart);
            _line.SetPosition(1, beamEnd);
        }
    }

    /// <summary>Nube visible más cercana a la mira dentro del cono de fijado (null si ninguna).</summary>
    private SmogCloud FindAssistTarget(Vector3 origin, Vector3 direction)
    {
        SmogCloud best = null;
        float bestScore = float.MaxValue;
        Vector3 flatAim = Flat(direction);
        float aimPitch = Pitch(direction);
        foreach (SmogCloud cloud in SmogCloud.Active)
        {
            Vector3 toCloud = cloud.transform.position - origin;
            float distance = toCloud.magnitude;
            if (distance > _range + cloud.Radius) continue;

            // Cono separado en horizontal y vertical
            float yaw = Vector3.Angle(flatAim, Flat(toCloud));
            float pitch = Mathf.Abs(Pitch(toCloud) - aimPitch);
            if (yaw > _assistAngle || pitch > _assistVerticalAngle) continue;
            float score = yaw / _assistAngle + pitch / _assistVerticalAngle;
            if (score >= bestScore) continue;

            // Sin edificios en medio
            float clear = Mathf.Max(0f, distance - cloud.Radius);
            if (clear > 0.1f && Physics.Raycast(origin, toCloud / distance, out RaycastHit hit, clear,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            bestScore = score;
            best = cloud;
        }
        return best;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // Grados por encima (+) o por debajo (−) del horizonte
    private static float Pitch(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;

    private void SetTarget(SmogCloud target)
    {
        if (target == CurrentTarget) return;
        if (CurrentTarget != null) CurrentTarget.SetTargeted(false);
        CurrentTarget = target;
        if (target != null) target.SetTargeted(true);
    }

    private void OnDisable() => SetTarget(null);

    /// <summary>Busca lo primero que toca el haz: la nube en la mira (o null) y la longitud del haz.</summary>
    private float Cast(Vector3 origin, Vector3 direction, out SmogCloud target)
    {
        int count = Physics.SphereCastNonAlloc(origin, _aimRadius, direction, _hits, _range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        SmogCloud closestCloud = null;
        float closest = _range;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (hit.collider.transform.IsChildOf(transform)) continue;

            var cloud = hit.collider.GetComponentInParent<SmogCloud>();
            if (cloud != null)
            {
                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    closestCloud = cloud;
                }
            }
            else if (!hit.collider.isTrigger && hit.distance < closest)
            {
                // Pared u otro sólido: el haz se corta ahí
                closest = hit.distance;
                closestCloud = null;
            }
        }

        target = closestCloud;
        // Barridos que empiezan dentro de un collider dan distancia 0: mostrar al menos un tramo corto
        return Mathf.Max(closest, 1f);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }
}
