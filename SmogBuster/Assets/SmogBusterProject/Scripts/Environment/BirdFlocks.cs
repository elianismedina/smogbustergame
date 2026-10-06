using UnityEngine;

/// <summary>
/// Bandadas de pájaros que vuelan sobre la ciudad cuando el aire está casi limpio.
/// Al bajar el smog hasta <see cref="_appearSmog"/> entran en espiral desde fuera de la ciudad y dan vueltas
/// sobre ella; si el smog vuelve a subir por encima de <see cref="_leaveSmog"/> se alejan y desaparecen.
/// Al ganar se quedan volando. Suena un loop de pájaros mientras están en el cielo.
/// El prefab de bandada solo aletea en el sitio: el vuelo lo hace este script moviendo la raíz.
/// </summary>
public class BirdFlocks : MonoBehaviour
{
    [SerializeField] private GameSession _session;

    [Header("Bandadas")]
    [Tooltip("Prefab de bandada (p. ej. 10BirdsPrefab de Low Poly Birds, versión URP). Si falta, no pasa nada.")]
    [SerializeField] private GameObject _flockPrefab;
    [SerializeField] private int _flockCount = 2;
    [Tooltip("Los pájaros del paquete miden unos 0,16 m de envergadura.")]
    [SerializeField] private float _flockScale = 8f;
    [Tooltip("Giro extra para que el pico mire hacia donde vuela.")]
    [SerializeField] private float _modelYaw;

    [Header("Umbrales de smog (0-1)")]
    [Tooltip("Aparecen al bajar el smog hasta este nivel.")]
    [Range(0f, 1f)]
    [SerializeField] private float _appearSmog = 0.2f;
    [Tooltip("Se van si el smog sube por encima de este nivel (si no, entrarían y saldrían sin parar al rozar el 20%).")]
    [Range(0f, 1f)]
    [SerializeField] private float _leaveSmog = 0.3f;

    [Header("Vuelo")]
    [Tooltip("Centro de las vueltas. Vacío = este objeto.")]
    [SerializeField] private Transform _center;
    [Tooltip("Radio de las vueltas de la primera bandada y de la última.")]
    [SerializeField] private Vector2 _radius = new Vector2(55f, 95f);
    [Tooltip("Altura de la primera bandada y de la última.")]
    [SerializeField] private Vector2 _height = new Vector2(24f, 32f);
    [Tooltip("Velocidad de vuelo en m/s.")]
    [SerializeField] private float _speed = 9f;
    [Tooltip("Distancia extra fuera del círculo desde la que entran y a la que se van.")]
    [SerializeField] private float _offscreenDistance = 160f;
    [Tooltip("Segundos que tardan en entrar o en irse.")]
    [SerializeField] private float _transitionTime = 10f;
    [SerializeField] private float _bobAmplitude = 1.5f;
    [SerializeField] private float _bobFrequency = 0.25f;

    [Header("Sonido")]
    [SerializeField] private AudioClip _birdsLoop;
    [SerializeField] private float _birdsVolume = 0.5f;
    [SerializeField] private float _audioFade = 2f;

    private class Flock
    {
        public Transform Root;
        public float Angle;
        public float Radius;
        public float Height;
        public float Direction;
        public float BobPhase;
        public float Presence; // 0 = fuera de la ciudad, 1 = dando vueltas
    }

    private Flock[] _flocks = new Flock[0];
    private bool _inSky;
    private AudioSource _audio;

    private void Start()
    {
        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_center == null) _center = transform;
        if (_flockPrefab == null) return;

        _flocks = new Flock[Mathf.Max(0, _flockCount)];
        for (int i = 0; i < _flocks.Length; i++)
        {
            float t = _flocks.Length > 1 ? i / (_flocks.Length - 1f) : 0f;
            _flocks[i] = new Flock
            {
                Root = CreateFlock(i),
                Angle = i * Mathf.PI * 2f / _flocks.Length + Random.Range(-0.3f, 0.3f),
                Radius = Mathf.Lerp(_radius.x, _radius.y, t),
                Height = Mathf.Lerp(_height.x, _height.y, t),
                Direction = i % 2 == 0 ? 1f : -1f,
                BobPhase = Random.Range(0f, Mathf.PI * 2f),
            };
        }

        if (_birdsLoop != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = _birdsLoop;
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0f;
            if (AudioManager.Instance != null) _audio.outputAudioMixerGroup = AudioManager.Instance.AmbienceGroup;
        }
    }

    private Transform CreateFlock(int index)
    {
        GameObject flock = Instantiate(_flockPrefab, transform);
        flock.name = $"BirdFlock_{index + 1}";
        flock.transform.localScale = Vector3.one * _flockScale;

        // Ligeros para móvil y WebGL: sin sombras, un hueso por vértice y sin animar fuera de cámara
        foreach (SkinnedMeshRenderer skin in flock.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.quality = SkinQuality.Bone1;
            skin.updateWhenOffscreen = false;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.receiveShadows = false;
            skin.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            skin.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        foreach (Animator animator in flock.GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            animator.speed = Random.Range(0.9f, 1.1f);
        }

        flock.SetActive(false);
        return flock.transform;
    }

    private void Update()
    {
        if (_flocks.Length == 0 || _session == null) return;

        // Al ganar se quedan como estén; al perder el smog estará alto y se irán
        if (!_session.IsOver || _session.Outcome != GameSession.Result.Victory)
        {
            if (!_inSky && _session.Smog <= _appearSmog) _inSky = true;
            else if (_inSky && _session.Smog > _leaveSmog) _inSky = false;
        }

        float step = Time.deltaTime / Mathf.Max(0.01f, _transitionTime);
        foreach (Flock flock in _flocks) Fly(flock, step);

        UpdateAudio();
    }

    private void Fly(Flock flock, float step)
    {
        flock.Presence = Mathf.MoveTowards(flock.Presence, _inSky ? 1f : 0f, step);
        bool visible = flock.Presence > 0f;
        if (flock.Root.gameObject.activeSelf != visible)
        {
            flock.Root.gameObject.SetActive(visible);
            if (visible)
            {
                // Desfase del aleteo para que las bandadas no vayan sincronizadas
                foreach (Animator animator in flock.Root.GetComponentsInChildren<Animator>())
                    animator.Play(0, 0, Random.value);
            }
        }
        if (!visible) return;

        // Entran y salen en espiral: el radio se abre hacia fuera de la ciudad
        float ease = Mathf.SmoothStep(0f, 1f, flock.Presence);
        float radius = flock.Radius + _offscreenDistance * (1f - ease);
        flock.Angle += flock.Direction * _speed / radius * Time.deltaTime;

        Vector3 offset = new Vector3(Mathf.Cos(flock.Angle), 0f, Mathf.Sin(flock.Angle)) * radius;
        float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f + flock.BobPhase) * _bobAmplitude;
        Vector3 position = _center.position + offset + Vector3.up * (flock.Height + bob);

        Vector3 velocity = position - flock.Root.position;
        flock.Root.position = position;
        if (velocity.sqrMagnitude > 0.0001f)
            flock.Root.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, _modelYaw, 0f);
    }

    private void UpdateAudio()
    {
        if (_audio == null) return;

        float target = _inSky ? _birdsVolume : 0f;
        _audio.volume = Mathf.MoveTowards(_audio.volume, target, _birdsVolume * Time.deltaTime / Mathf.Max(0.01f, _audioFade));
        if (_audio.volume > 0f && !_audio.isPlaying) _audio.Play();
        else if (_audio.volume <= 0f && _audio.isPlaying) _audio.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = _center != null ? _center.position : transform.position;
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        DrawCircle(center + Vector3.up * _height.x, _radius.x);
        DrawCircle(center + Vector3.up * _height.y, _radius.y);
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int Segments = 48;
        Vector3 previous = center + Vector3.right * radius;
        for (int i = 1; i <= Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
