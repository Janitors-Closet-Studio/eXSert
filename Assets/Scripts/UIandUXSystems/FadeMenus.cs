using System.Collections;
using UnityEngine;

public class FadeMenus : MonoBehaviour
{
    [SerializeField] public float fadeDuration = 0.25f;

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
            Debug.Log($"Fading out {menu.name} over {duration} seconds.");

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
