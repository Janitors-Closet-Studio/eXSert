using System.Collections;
using UnityEngine;

public class FadeMenus : MonoBehaviour
{
    [SerializeField] public float fadeDuration = 0.25f;

    private readonly System.Collections.Generic.Dictionary<GameObject, Coroutine> activeFades = new();

    public void FadeMenuSafe(GameObject menu, float duration, bool turnOn)
    {
        if (menu == null) return;
        // Stop any existing fade coroutine for this menu
        if (activeFades.TryGetValue(menu, out var running))
        {
            if (running != null)
                StopCoroutine(running);
            activeFades.Remove(menu);
        }
        // Start and track the new fade coroutine
        Coroutine c = StartCoroutine(FadeMenuRoutine(menu, duration, turnOn));
        activeFades[menu] = c;
    }

    private IEnumerator FadeMenuRoutine(GameObject menu, float duration, bool turnOn)
    {
        yield return FadeMenu(menu, duration, turnOn);
        // Remove from active fades after done
        activeFades.Remove(menu);
    }

    public IEnumerator FadeMenu(GameObject menu, float duration, bool turnOn)
    {
        if (menu == null)
            yield break;

        CanvasGroup canvasGroup = menu.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = menu.AddComponent<CanvasGroup>();
        }
        
        float elapsed = 0f;

        if (turnOn)
        {
            menu.SetActive(true);
            canvasGroup.alpha = 0f;
            Debug.Log($"Fading in {menu.name} over {duration} seconds.");

            while(elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }
        else
        {
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.UI, $"Fading out {menu.name} over {duration} seconds.");

            while(elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            menu.SetActive(false);
        }
        
    }
}
