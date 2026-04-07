    
using UnityEngine;
using System.Collections;

public class StrictFrameLimiter : MonoBehaviour
{
    private GraphicsSettings graphicsSettings;
    public int targetFPS;
    private Coroutine limiterCoroutine;

    void Start()
    {
        graphicsSettings = this.GetComponent<GraphicsSettings>();
        if (graphicsSettings == null)
            graphicsSettings = GetComponentInChildren<GraphicsSettings>();

        targetFPS = PlayerPrefs.GetInt("masterFPS", 60);
        // Disable Unity's built-in limiter for strict control
        Application.targetFrameRate = 1000;
        QualitySettings.vSyncCount = 0;
        limiterCoroutine = StartCoroutine(FrameLimiter());
    }
    // Call this to update the FPS cap at runtime
    public void UpdateTargetFPS(int newFPS)
    {
        targetFPS = newFPS;
        Debug.Log($"[StrictFrameLimiter] Updated targetFPS to {targetFPS}");
    }

    IEnumerator FrameLimiter()
    {
        Debug.Log($"[StrictFrameLimiter] Starting frame limiter: targetFPS={targetFPS}, targetFrameRate={Application.targetFrameRate}, vSyncCount={QualitySettings.vSyncCount}");
        float targetDelta = 1f / targetFPS;
        while (true)
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitForEndOfFrame();
            float elapsed = Time.realtimeSinceStartup - start;
            float wait = targetDelta - elapsed;
            if (wait > 0)
                yield return new WaitForSecondsRealtime(wait);
        }
    }
}
