using UnityEngine;

/// <summary>
/// DEPRECATED: All calls now route to DebugLogSettingsM for centralized control.
/// Kept for backward compatibility only.
/// </summary>
public class EnemyBehaviorDebugLogBools : MonoBehaviour
{
    public static bool IsEnabled(string category)
    {
        return DebugLogSettingsM.IsEnabledByName(category);
    }

    public static void Log(string category, string message, Object context = null)
    {
        DebugLogSettingsM.LogByName(category, message, context);
    }

    public static void LogWarning(string category, string message, Object context = null)
    {
        DebugLogSettingsM.LogWarningByName(category, message, context);
    }

    public static void LogError(string message, Object context = null)
    {
        if (context != null) Debug.LogError(message, context);
        else Debug.LogError(message);
    }
}
