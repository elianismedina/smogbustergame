using UnityEngine;

/// <summary>
/// Cámara de persecución: se mantiene detrás del objetivo siguiendo su rumbo (yaw) con suavizado.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [Tooltip("Posición relativa al objetivo, en el marco de su rumbo (x lateral, y altura, z atrás es negativo).")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 3f, -7f);
    [Tooltip("Punto al que mira, relativo al objetivo.")]
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float _positionSmoothing = 6f;
    [SerializeField] private float _yawSmoothing = 3f;
    [SerializeField] private float _minHeight = 0.5f;

    private float _yaw;

    /// <summary>Si es false, la cámara mantiene su rumbo actual (p. ej. mientras el dron cae girando).</summary>
    public bool FollowYaw { get; set; } = true;

    private void Start()
    {
        if (_target == null)
        {
            Debug.LogWarning("FollowCamera: no hay objetivo asignado.", this);
            enabled = false;
            return;
        }

        _yaw = _target.eulerAngles.y;
        transform.position = DesiredPosition();
        transform.LookAt(_target.position + _lookOffset);
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (FollowYaw)
        {
            _yaw = Mathf.LerpAngle(_yaw, _target.eulerAngles.y, 1f - Mathf.Exp(-_yawSmoothing * dt));
        }

        Vector3 desired = DesiredPosition();
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_positionSmoothing * dt));
        transform.LookAt(_target.position + _lookOffset);
    }

    private Vector3 DesiredPosition()
    {
        Vector3 position = _target.position + Quaternion.Euler(0f, _yaw, 0f) * _offset;
        position.y = Mathf.Max(position.y, _minHeight);
        return position;
    }
}
