using System;
using UnityEngine;

/// <summary>
/// Detecta choques contra objetos con <see cref="CrashHazard"/> (edificios).
/// Al chocar: corta el control, activa la gravedad, hace caer el dron girando y lanza <see cref="Crashed"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadcopterCrash : MonoBehaviour
{
    [Tooltip("Velocidad mínima de impacto (m/s) para estrellarse. 0 = cualquier contacto.")]
    [SerializeField] private float _minImpactSpeed = 0f;
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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<QuadcopterController>();
        _input = GetComponent<QuadInput>();
        _visualTilt = GetComponentInChildren<QuadVisualTilt>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (HasCrashed) return;
        if (collision.collider.GetComponentInParent<CrashHazard>() == null) return;
        if (collision.relativeVelocity.magnitude < _minImpactSpeed) return;

        Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -transform.forward;
        Crash(normal);
    }

    private void Crash(Vector3 hitNormal)
    {
        HasCrashed = true;

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
