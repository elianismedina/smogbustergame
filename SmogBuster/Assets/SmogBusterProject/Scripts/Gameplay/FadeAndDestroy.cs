using UnityEngine;

/// <summary>
/// Marca temporal (p. ej. la X de una semilla fallida): sube un poco, mira a la cámara,
/// se encoge y desaparece.
/// </summary>
public class FadeAndDestroy : MonoBehaviour
{
    [SerializeField] private float _lifetime = 1.2f;
    [SerializeField] private float _riseSpeed = 0.6f;

    private Vector3 _startScale;
    private float _age;

    private void Awake() => _startScale = transform.localScale;

    private void Update()
    {
        _age += Time.deltaTime;
        transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        float k = Mathf.Clamp01(_age / _lifetime);
        transform.localScale = _startScale * (1f - k * k);
        if (_age >= _lifetime) Destroy(gameObject);
    }
}
