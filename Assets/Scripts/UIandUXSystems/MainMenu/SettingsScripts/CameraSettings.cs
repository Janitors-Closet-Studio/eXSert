using Unity.Cinemachine;
using UnityEngine;

public class CameraSettings : MonoBehaviour
{
    private void Awake()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.UpdatePlayerCameraSens(SettingsManager.Instance.sensitivity);
            SettingsManager.Instance.UpdatePlayerInvertY(SettingsManager.Instance.invertY);
        }
    }

}
