using UnityEngine;

/// <summary>
/// Gira lentamente el skybox (propiedad _Rotation de Skybox/Panoramic o Skybox/Cubemap)
/// para que las nubes parezcan desplazarse. Trabaja sobre una copia del material
/// para no modificar el asset original.
/// </summary>
public class SkyboxRotator : MonoBehaviour
{
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");

    [Tooltip("Grados por segundo.")]
    [SerializeField] private float _degreesPerSecond = 0.4f;

    private Material _original;
    private Material _instance;
    private float _rotation;

    private void Start()
    {
        _original = RenderSettings.skybox;
        if (_original == null || !_original.HasProperty(RotationId))
        {
            enabled = false;
            return;
        }

        _instance = new Material(_original);
        _rotation = _original.GetFloat(RotationId);
        RenderSettings.skybox = _instance;
    }

    private void Update()
    {
        _rotation = Mathf.Repeat(_rotation + _degreesPerSecond * Time.deltaTime, 360f);
        _instance.SetFloat(RotationId, _rotation);
    }

    private void OnDestroy()
    {
        if (_instance == null) return;
        if (RenderSettings.skybox == _instance) RenderSettings.skybox = _original;
        Destroy(_instance);
    }
}
