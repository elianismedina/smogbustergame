using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vegetación en dos estados, marchita y florecida (GDD 8.6 y 9.1).
/// Cada grupo de renderers pasa de su color marchito a su color sano según:
/// - el smog global (como el cielo en <see cref="SkyPurification"/>), y
/// - las semillas: lo que esté cerca de un punto de siembra ya sembrado revive en parte enseguida.
/// Las copas pueden crecer un poco y echar flores al revivir del todo.
/// Los colores se cambian con MaterialPropertyBlock, sin tocar los materiales.
/// </summary>
public class VegetationRevival : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Serializable]
    private class Group
    {
        public string Name;
        public Renderer[] Renderers;
        public Color Withered = new Color(0.45f, 0.42f, 0.36f);
        public Color Healthy = new Color(0.30f, 0.68f, 0.25f);
        [Tooltip("Escala relativa de marchito (p. ej. 0.85: la copa crece un 15% al revivir). 1 = no cambia.")]
        public float WitheredScale = 1f;
        [Tooltip("Echa flores al revivir del todo (necesita el material de flor).")]
        public bool Blossoms;
    }

    [SerializeField] private GameSession _session;
    [SerializeField] private Group[] _groups = Array.Empty<Group>();

    [Header("Umbrales de smog (0-1)")]
    [Tooltip("Por encima de este nivel la vegetación está totalmente marchita.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dirtySmog = 0.6f;
    [Tooltip("Por debajo de este nivel está totalmente florecida.")]
    [Range(0f, 1f)]
    [SerializeField] private float _cleanSmog = 0.05f;
    [SerializeField] private float _smoothing = 1.2f;

    [Header("Semillas")]
    [Tooltip("Distancia a un punto sembrado dentro de la que la vegetación revive en parte.")]
    [SerializeField] private float _plantRadius = 22f;
    [Tooltip("Cuánto revive (0-1) lo que está cerca de un punto sembrado.")]
    [Range(0f, 1f)]
    [SerializeField] private float _plantBoost = 0.5f;

    [Header("Flores")]
    [SerializeField] private Material _blossomMaterial;
    [SerializeField] private int _blossomsPerCanopy = 7;
    [SerializeField] private float _blossomSize = 0.35f;
    [Tooltip("Revivido (0-1) a partir del que se abren las flores.")]
    [Range(0f, 1f)]
    [SerializeField] private float _blossomFrom = 0.65f;

    private class Item
    {
        public Group Group;
        public Renderer Renderer;
        public Vector3 BaseScale;
        public float Revival;
        public float Boost;
        public List<Transform> Blossoms;
    }

    private readonly List<Item> _items = new List<Item>();
    private MaterialPropertyBlock _block;

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        _block = new MaterialPropertyBlock();

        foreach (Group group in _groups)
        {
            if (group.Renderers == null) continue;
            foreach (Renderer renderer in group.Renderers)
            {
                if (renderer == null) continue;
                var item = new Item { Group = group, Renderer = renderer, BaseScale = renderer.transform.localScale };
                if (group.Blossoms && _blossomMaterial != null) item.Blossoms = CreateBlossoms(renderer);
                _items.Add(item);
                Apply(item);
            }
        }
    }

    private void OnEnable() => PlantingSpot.Planted += OnPlanted;

    private void OnDisable() => PlantingSpot.Planted -= OnPlanted;

    // Lo cercano al punto sembrado revive en parte, aunque el smog siga alto
    private void OnPlanted(PlantingSpot spot)
    {
        foreach (Item item in _items)
        {
            if (Vector3.Distance(item.Renderer.bounds.center, spot.transform.position) <= _plantRadius)
            {
                item.Boost = Mathf.Max(item.Boost, _plantBoost);
            }
        }
    }

    private void Update()
    {
        float global = _session != null
            ? Mathf.InverseLerp(_dirtySmog, _cleanSmog, _session.Smog)
            : 0f;
        float k = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);

        foreach (Item item in _items)
        {
            float target = Mathf.Max(global, item.Boost);
            if (Mathf.Abs(item.Revival - target) < 0.001f) continue;
            item.Revival = Mathf.Lerp(item.Revival, target, k);
            Apply(item);
        }
    }

    private void Apply(Item item)
    {
        float t = item.Revival;
        Color color = Color.Lerp(item.Group.Withered, item.Group.Healthy, t);
        item.Renderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, color);
        _block.SetColor(ColorId, color);
        item.Renderer.SetPropertyBlock(_block);

        if (!Mathf.Approximately(item.Group.WitheredScale, 1f))
        {
            item.Renderer.transform.localScale = item.BaseScale * Mathf.Lerp(item.Group.WitheredScale, 1f, t);
        }

        if (item.Blossoms != null)
        {
            float open = Mathf.InverseLerp(_blossomFrom, 1f, t);
            foreach (Transform blossom in item.Blossoms)
            {
                blossom.localScale = Vector3.one * (_blossomSize * open);
                blossom.gameObject.SetActive(open > 0.01f);
            }
        }
    }

    // Flores repartidas sobre la parte alta de la copa. Van en la escena, no bajo la copa, para no heredar su escala.
    private List<Transform> CreateBlossoms(Renderer canopy)
    {
        var list = new List<Transform>();
        Bounds bounds = canopy.bounds;
        var random = new System.Random(canopy.name.GetHashCode() ^ canopy.transform.position.GetHashCode());

        for (int i = 0; i < _blossomsPerCanopy; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float up = 0.15f + 0.7f * (float)random.NextDouble();
            Vector3 dir = new Vector3(Mathf.Cos(angle) * Mathf.Sqrt(1f - up * up), up, Mathf.Sin(angle) * Mathf.Sqrt(1f - up * up));
            Vector3 point = bounds.center + Vector3.Scale(dir, bounds.extents * 0.95f);

            GameObject blossom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(blossom.GetComponent<Collider>());
            blossom.name = "Blossom";
            blossom.transform.SetParent(transform, true);
            blossom.transform.position = point;
            Renderer r = blossom.GetComponent<Renderer>();
            r.sharedMaterial = _blossomMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blossom.SetActive(false);
            list.Add(blossom.transform);
        }
        return list;
    }
}
