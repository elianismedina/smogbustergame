using System;
using UnityEngine;

/// <summary>
/// Detecta choques contra objetos con <see cref="CrashHazard"/> (edificios).
/// Posarse encima (contacto por debajo, superficie casi horizontal y descenso suave) es un aterrizaje, no un choque.
/// Al chocar: corta el control, activa la gravedad, hace caer el dron girando y lanza <see cref="Crashed"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadcopterCrash : MonoBehaviour
{
    public enum CrashReason
    {
        None,
        HitObstacle,
        HardLanding,
    }

    [Header("Choque lateral")]
    [Tooltip("Velocidad mínima de impacto contra una pared (m/s) para estrellarse. 0 = cualquier contacto.")]
    [SerializeField] private float _minImpactSpeed = 0f;

    [Header("Aterrizaje")]
    [Tooltip("Componente Y mínima de la normal para considerar el contacto como suelo (0.7 ≈ pendiente de 45°).")]
    [SerializeField] private float _landingNormalMinY = 0.7f;
    [Tooltip("Velocidad de descenso máxima (m/s) para aterrizar sin romperse.")]
    [SerializeField] private float _maxLandingSpeed = 7f;

    [Header("Caída")]
    [Tooltip("Empuje hacia fuera del edificio al chocar (m/s).")]
    [SerializeField] private float _bounceSpeed = 2f;
    [Tooltip("Giro aleatorio al caer (rad/s).")]
    [SerializeField] private float _tumbleSpeed = 6f;

    private Rigidbody _rb;
    private QuadcopterController _controller;
    private QuadInput _input;
    private QuadVisualTilt _visualTilt;

    /// <summary>Se lanza una sola vez, en el momento del choque.</summary>
    public event Action Crashed;

    public bool HasCrashed { get; private set; }

    public CrashReason LastCrashReason { get; private set; }

    /// <summary>True mientras el dron está posado sobre un edificio.</summary>
    public bool IsLanded { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<QuadcopterController>();
        _input = GetComponent<QuadInput>();
        _visualTilt = GetComponentInChildren<QuadVisualTilt>();
    }

    private void FixedUpdate()
    {
        // Se recalcula en cada paso de física a partir de OnCollisionStay
        IsLanded = false;
    }

    private void OnCollisionEnter(Collision collision) => Evaluate(collision, isNewContact: true);

    // Cubre contactos laterales que aparecen mientras el dron ya toca el mismo edificio
    private void OnCollisionStay(Collision collision) => Evaluate(collision, isNewContact: false);

    private void Evaluate(Collision collision, bool isNewContact)
    {
        if (HasCrashed) return;
        if (collision.collider.GetComponentInParent<CrashHazard>() == null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            // Velocidad con la que el dron se acerca a la superficie, a lo largo de su normal
            float approachSpeed = Vector3.Dot(collision.relativeVelocity, contact.normal);

            if (contact.normal.y >= _landingNormalMinY)
            {
                // Superficie por debajo: aterrizaje, salvo que llegue demasiado rápido
                if (isNewContact && approachSpeed > _maxLandingSpeed)
                {
                    Crash(contact.normal, CrashReason.HardLanding);
                    return;
                }
                IsLanded = true;
                continue;
            }

            // Pared o techo: choque
            bool hitHardEnough = approachSpeed >= _minImpactSpeed;
            bool anyTouchCounts = isNewContact && _minImpactSpeed <= 0f;
            if (hitHardEnough || anyTouchCounts)
            {
                Crash(contact.normal, CrashReason.HitObstacle);
                return;
            }
        }
    }

    private void Crash(Vector3 hitNormal, CrashReason reason)
    {
        HasCrashed = true;
        IsLanded = false;
        LastCrashReason = reason;

        // Sin control: el dron pasa a ser un objeto físico que cae
        if (_controller != null) _controller.enabled = false;
        if (_input != null) _input.enabled = false;
        if (_visualTilt != null) _visualTilt.enabled = false;

        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.None;
        _rb.AddForce(hitNormal * _bounceSpeed, ForceMode.VelocityChange);
        _rb.AddTorque(UnityEngine.Random.onUnitSphere * _tumbleSpeed, ForceMode.VelocityChange);

        Crashed?.Invoke();
    }
}
