using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Extensión de Cinemachine: no deja bajar la cámara de una altura mínima (p. ej. al volar a ras de suelo).
/// Va después del Deoccluder, así se respeta también tras apartarse de los edificios.
/// </summary>
[AddComponentMenu("Cinemachine/Procedural/Extensions/Camera Min Height")]
public class CameraMinHeight : CinemachineExtension
{
    [SerializeField] private float _minHeight = 0.5f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;
        float y = state.GetCorrectedPosition().y;
        if (y < _minHeight) state.PositionCorrection += Vector3.up * (_minHeight - y);
    }
}
