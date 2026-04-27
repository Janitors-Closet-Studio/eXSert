using UnityEngine;

public class DebugActiveLogger : MonoBehaviour
{
    [Tooltip("Enable verbose active/deactive debug logs for this object.")]
    [SerializeField] private bool debugLogging = false;

    void OnDisable()
    {
        if (debugLogging) Debug.Log($"[DebugActiveLogger] {gameObject.name} was deactivated!", this);
    }
    void OnEnable()
    {
        if (debugLogging) Debug.Log($"[DebugActiveLogger] {gameObject.name} was activated!", this);
    }
}