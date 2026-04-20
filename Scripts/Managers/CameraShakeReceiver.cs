using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraShakeReceiver : MonoBehaviour
{
    [SerializeField] private Vector2 positionScale = Vector2.one;
    [SerializeField] private float rotationScale = 1f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private CinemachineVirtualCamera virtualCamera;
    private float initialDutch;
    private bool hasVirtualCamera;
    private bool initialized;

    private void OnEnable()
    {
        if (!initialized)
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            hasVirtualCamera = TryGetComponent(out virtualCamera);

            if (hasVirtualCamera)
                initialDutch = virtualCamera.m_Lens.Dutch;

            initialized = true;
        }

        CameraShakeManager.OnShakeUpdated += ApplyShake;
    }

    private void OnDisable()
    {
        CameraShakeManager.OnShakeUpdated -= ApplyShake;

        if (!initialized)
            return;

        transform.localPosition = initialLocalPosition;

        if (hasVirtualCamera)
        {
            LensSettings lens = virtualCamera.m_Lens;
            lens.Dutch = initialDutch;
            virtualCamera.m_Lens = lens;
        }
        else
        {
            transform.localRotation = initialLocalRotation;
        }
    }

    private void ApplyShake(CameraShakeManager.ShakeFrameData data)
    {
        Vector3 offset = new Vector3(
            data.PositionOffset.x * positionScale.x,
            data.PositionOffset.y * positionScale.y,
            0f
        );

        transform.localPosition = initialLocalPosition + offset;

        float rotationOffset = data.RotationOffset * rotationScale;

        if (hasVirtualCamera)
        {
            LensSettings lens = virtualCamera.m_Lens;
            lens.Dutch = initialDutch + rotationOffset;
            virtualCamera.m_Lens = lens;
        }
        else
        {
            transform.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        }
    }
}
