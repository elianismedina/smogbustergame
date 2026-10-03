using System.Collections;
using UnityEngine;

/// <summary>
/// Zumbido de motores del quadcopter: un loop 3D cuyo tono y volumen siguen
/// la velocidad horizontal y el ascenso. Arranca con "spin-up" y, al estrellarse,
/// el motor se apaga bajando de tono.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadcopterAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _motorLoop;

    [Header("Tono")]
    [SerializeField] private float _idlePitch = 0.9f;
    [SerializeField] private float _maxSpeedPitch = 1.35f;
    [Tooltip("Tono extra al subir (+) y al bajar (-), por m/s vertical.")]
    [SerializeField] private float _climbPitchPerMeter = 0.03f;

    [Header("Volumen")]
    [SerializeField] private float _idleVolume = 0.55f;
    [SerializeField] private float _maxSpeedVolume = 0.9f;
    [SerializeField] private float _smoothing = 6f;

    [Header("Arranque y apagado")]
    [SerializeField] private float _spinUpDuration = 0.8f;
    [SerializeField] private float _powerDownDuration = 0.7f;

    private Rigidbody _rb;
    private QuadcopterController _controller;
    private QuadcopterCrash _crash;
    private GameSession _session;
    private AudioSource _source;
    private float _spinUp;
    private bool _poweringDown;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<QuadcopterController>();
        _crash = GetComponent<QuadcopterCrash>();

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = _motorLoop;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0.8f; // casi 3D: se ubica en el espacio pero nunca desaparece del todo
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
        _source.minDistance = 6f;
        _source.maxDistance = 80f;
        _source.dopplerLevel = 0.3f;
        _source.volume = 0f;
        _source.pitch = _idlePitch * 0.5f;
    }

    private void Start()
    {
        // El AudioManager se crea antes de la primera escena; el grupo SFX respeta el slider de efectos
        if (AudioManager.Instance != null) _source.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
        if (_motorLoop != null) _source.Play();
    }

    private void OnEnable()
    {
        if (_crash != null) _crash.Crashed += OnCrashed;
        _session = FindAnyObjectByType<GameSession>();
        if (_session != null) _session.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        if (_crash != null) _crash.Crashed -= OnCrashed;
        if (_session != null) _session.Ended -= OnSessionEnded;
    }

    // Al terminar la partida (victoria o derrota) los motores se apagan igual que al chocar
    private void OnSessionEnded(GameSession.Result result) => OnCrashed();

    private void Update()
    {
        if (_poweringDown || _motorLoop == null) return;

        _spinUp = Mathf.MoveTowards(_spinUp, 1f, Time.deltaTime / Mathf.Max(0.01f, _spinUpDuration));

        float maxSpeed = _controller != null ? _controller.MaxSpeed : 12f;
        float horizontal = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
        float speed01 = Mathf.Clamp01(horizontal / Mathf.Max(0.01f, maxSpeed));
        float vertical = _rb.linearVelocity.y;

        float targetPitch = Mathf.Lerp(_idlePitch, _maxSpeedPitch, speed01)
                            + Mathf.Clamp(vertical * _climbPitchPerMeter, -0.1f, 0.15f);
        float targetVolume = Mathf.Lerp(_idleVolume, _maxSpeedVolume, speed01);

        // Durante el arranque el tono sube desde la mitad
        targetPitch *= Mathf.Lerp(0.5f, 1f, _spinUp);
        targetVolume *= _spinUp;

        float k = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        _source.pitch = Mathf.Lerp(_source.pitch, targetPitch, k);
        _source.volume = Mathf.Lerp(_source.volume, targetVolume, k);
    }

    private void OnCrashed()
    {
        if (_poweringDown) return;
        _poweringDown = true;
        StartCoroutine(PowerDown());
    }

    private IEnumerator PowerDown()
    {
        float startPitch = _source.pitch;
        float startVolume = _source.volume;
        for (float t = 0f; t < _powerDownDuration; t += Time.deltaTime)
        {
            float k = t / _powerDownDuration;
            _source.pitch = Mathf.Lerp(startPitch, startPitch * 0.35f, k);
            _source.volume = Mathf.Lerp(startVolume, 0f, k * k);
            yield return null;
        }
        _source.Stop();
    }
}
