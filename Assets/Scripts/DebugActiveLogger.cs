using UnityEngine;

public class DebugActiveLogger : MonoBehaviour
{
    void OnDisable()
    {
        Debug.Log($"[DebugActiveLogger] {gameObject.name} was deactivated!", this);
    }
    void OnEnable()
    {
        Debug.Log($"[DebugActiveLogger] {gameObject.name} was activated!", this);
    }
}