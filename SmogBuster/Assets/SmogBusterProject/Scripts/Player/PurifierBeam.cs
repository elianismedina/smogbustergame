using UnityEngine;

/// <summary>
/// Rayo Purificador (GDD 6.1): mientras se mantiene pulsado, lanza un haz hacia delante desde el
/// cañón azul. La primera nube de smog que toca se disipa al instante; un edificio corta el haz.
/// Usa un barrido esférico para que no haga falta apuntar con precisión en pantalla táctil.
/// </summary>
[RequireComponent(typeof(QuadInput))]
public class PurifierBeam : MonoBehaviour
{
    [Tooltip("Punta del cañón azul. Si se deja vacío, se busca un hijo llamado Cannon_Beam.")]
    [SerializeField] private Transform _muzzle;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _range = 30f;
    [Tooltip("Radio del barrido: tolerancia al apuntar.")]
    [SerializeField] private float _aimRadius = 1.5f;
    [Tooltip("Grados hacia abajo respecto al frente del dron.")]
    [SerializeField] private float _downAngle = 4f;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private QuadInput _input;
    private QuadcopterCrash _crash;

    /// <summary>True mientras el haz está activo (para sonido y efectos).</summary>
    public bool IsFiring { get; private set; }

    private void Awake()
    {
        _input = GetComponent<QuadInput>();
        _crash = GetComponent<QuadcopterCrash>();
        if (_muzzle == null) _muzzle = FindChild(transform, "Cannon_Beam");
        if (_line != null) _line.enabled = false;
    }

    private void Update()
    {
        bool crashed = _crash != null && _crash.HasCrashed;
        IsFiring = _input.Beam && !crashed && Time.timeScale > 0f;
        if (_line != null) _line.enabled = IsFiring;
        if (!IsFiring) return;

        Vector3 origin = _muzzle != null ? _muzzle.position : transform.position;
        // El cañón apunta según el rumbo del dron, no según su inclinación al moverse
        Vector3 forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        Vector3 direction = Quaternion.AngleAxis(_downAngle, Vector3.Cross(Vector3.up, forward)) * forward;

        float end = Cast(origin, direction);
        if (_line != null)
        {
            _line.SetPosition(0, origin);
            _line.SetPosition(1, origin + direction * end);
        }
    }

    /// <summary>Busca lo primero que toca el haz. Devuelve la longitud del haz.</summary>
    private float Cast(Vector3 origin, Vector3 direction)
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

        if (closestCloud != null) closestCloud.Purify();
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
