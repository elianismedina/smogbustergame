using UnityEngine;

/// <summary>
/// Efectos visuales de las acciones (GDD 7.3 y 9.1), con sistemas de partículas creados por código:
/// - Rayo: el haz late en grosor, brilla en la punta del cañón y suelta chispas cian donde impacta;
///   al disipar una nube, estalla en destellos cian.
/// - Semilla: bocanada verde al salir del cañón y estela verde detrás de la cápsula.
/// - Florecimiento: hojas verdes y pétalos rosas que saltan del punto sembrado, con un anillo de luz.
/// Va en el dron. Todos los efectos usan el mismo material aditivo (el color va en cada partícula).
/// </summary>
public class GameplayVfx : MonoBehaviour
{
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private LineRenderer _beamLine;

    [Header("Rayo")]
    [SerializeField] private Color _beamColor = new Color(0.35f, 0.9f, 1f);
    [Tooltip("Cuánto late el grosor del haz (0 = nada).")]
    [SerializeField] private float _beamPulse = 0.35f;
    [SerializeField] private float _beamPulseSpeed = 30f;

    [Header("Semilla")]
    [SerializeField] private Color _seedColor = new Color(0.45f, 0.95f, 0.4f);

    [Header("Florecimiento")]
    [SerializeField] private Color _leafColor = new Color(0.4f, 0.9f, 0.35f);
    [SerializeField] private Color _petalColor = new Color(1f, 0.62f, 0.8f);

    private PurifierBeam _beam;
    private SeedLauncher _seeds;
    private Transform _beamMuzzle;
    private Transform _seedMuzzle;
    private float _beamWidth = 1f;

    private ParticleSystem _muzzleGlow;
    private ParticleSystem _impact;
    private ParticleSystem _cloudBurst;
    private ParticleSystem _seedPuff;
    private ParticleSystem _bloomLeaves;
    private ParticleSystem _bloomRing;

    private void Awake()
    {
        _beam = GetComponent<PurifierBeam>();
        _seeds = GetComponent<SeedLauncher>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cannon_Beam") _beamMuzzle = t;
            if (t.name == "Cannon_Seed") _seedMuzzle = t;
        }
        if (_beamLine == null) _beamLine = GetComponentInChildren<LineRenderer>(true);
        if (_beamLine != null) _beamWidth = _beamLine.widthMultiplier;
        if (_glowMaterial == null) return;

        _muzzleGlow = Create("VFX_BeamMuzzle", 0.12f, 0.3f, 0.2f, 0.45f, 0f, _beamColor, 16);
        _impact = Create("VFX_BeamImpact", 0.2f, 0.45f, 1.5f, 4f, 0f, _beamColor, 64, 0.12f, 0.25f);
        _cloudBurst = Create("VFX_CloudBurst", 0.5f, 1f, 3f, 8f, -0.2f, _beamColor, 96, 0.35f, 0.8f, 1.2f);
        _seedPuff = Create("VFX_SeedPuff", 0.25f, 0.45f, 1f, 3f, 0f, _seedColor, 32, 0.15f, 0.3f);
        _bloomLeaves = Create("VFX_BloomLeaves", 0.9f, 1.5f, 2.5f, 6f, 0.6f, _leafColor, 96, 0.25f, 0.5f, 0.4f);
        _bloomRing = Create("VFX_BloomRing", 0.6f, 0.6f, 0f, 0f, 0f, _leafColor, 4, 1f, 1f);
        ParticleSystem.SizeOverLifetimeModule ring = _bloomRing.sizeOverLifetime;
        ring.size = new ParticleSystem.MinMaxCurve(6f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
        // Destello plano sobre el suelo que se abre
        _bloomRing.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    private void OnEnable()
    {
        SmogCloud.Purified += OnCloudPurified;
        PlantingSpot.Planted += OnPlanted;
        if (_seeds != null) _seeds.Launched += OnSeedLaunched;
    }

    private void OnDisable()
    {
        SmogCloud.Purified -= OnCloudPurified;
        PlantingSpot.Planted -= OnPlanted;
        if (_seeds != null) _seeds.Launched -= OnSeedLaunched;
    }

    private void LateUpdate()
    {
        bool firing = _beam != null && _beam.IsFiring;

        if (_beamLine != null && firing)
        {
            _beamLine.widthMultiplier = _beamWidth * (1f + _beamPulse * Mathf.Sin(Time.time * _beamPulseSpeed));
        }

        if (!firing || _glowMaterial == null) return;

        // Brillo en la punta del cañón y chispas en el extremo del haz, cada frame mientras dispara
        if (_beamMuzzle != null)
        {
            _muzzleGlow.transform.position = _beamMuzzle.position;
            _muzzleGlow.Emit(1);
        }
        if (_beamLine != null && _beamLine.positionCount > 1)
        {
            _impact.transform.position = _beamLine.GetPosition(_beamLine.positionCount - 1);
            _impact.Emit(2);
        }
    }

    private void OnCloudPurified(SmogCloud cloud)
    {
        if (_cloudBurst == null) return;
        _cloudBurst.transform.position = cloud.transform.position;
        _cloudBurst.Emit(40);
    }

    private void OnSeedLaunched()
    {
        if (_seedPuff == null || _seedMuzzle == null) return;
        _seedPuff.transform.position = _seedMuzzle.position;
        _seedPuff.Emit(14);
    }

    private void OnPlanted(PlantingSpot spot)
    {
        if (_bloomLeaves == null) return;
        Vector3 at = spot.transform.position + Vector3.up * 0.6f;
        _bloomLeaves.transform.position = at;
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
        emit.startColor = _leafColor;
        _bloomLeaves.Emit(emit, 30);
        emit.startColor = _petalColor;
        _bloomLeaves.Emit(emit, 18);

        _bloomRing.transform.position = spot.transform.position + Vector3.up * 0.15f;
        _bloomRing.Emit(1);
    }

    // Partículas en espacio de mundo que se emiten a mano (sin emisión automática)
    private ParticleSystem Create(string objectName, float minLife, float maxLife, float minSpeed, float maxSpeed,
        float gravity, Color color, int maxParticles, float minSize = 0.25f, float maxSize = 0.45f, float radius = 0.05f)
    {
        var go = new GameObject(objectName);
        // Fuera del dron: sus partículas no deben heredar su movimiento ni ocultarse en primera persona
        go.transform.SetParent(null, false);
        Own(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        // Se apagan desvaneciéndose
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    // Los sistemas viven en la raíz de la escena; se destruyen con el dron
    private readonly System.Collections.Generic.List<GameObject> _owned = new System.Collections.Generic.List<GameObject>();

    private void Own(GameObject go) => _owned.Add(go);

    private void OnDestroy()
    {
        foreach (GameObject go in _owned)
        {
            if (go != null) Destroy(go);
        }
    }
}
