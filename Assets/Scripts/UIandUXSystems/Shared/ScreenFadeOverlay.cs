using System.Collections;
using Singletons;
using UnityEngine;
using UnityEngine.UI;

public sealed class ScreenFadeOverlay : Singleton<ScreenFadeOverlay>
{
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private int sortingOrder = 5000;

    private Canvas overlayCanvas;
    private CanvasGroup overlayCanvasGroup;
    private Image overlayImage;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
            return;

        EnsureOverlay();
    }

    public IEnumerator FadeTo(float targetAlpha, float durationSeconds)
    {
        EnsureOverlay();

        float startAlpha = overlayCanvasGroup.alpha;
        if (durationSeconds <= 0f)
        {
            overlayCanvasGroup.alpha = Mathf.Clamp01(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        overlayCanvasGroup.alpha = Mathf.Clamp01(targetAlpha);
    }

    public void SetImmediate(float alpha)
    {
        EnsureOverlay();
        overlayCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void EnsureOverlay()
    {
        if (overlayCanvasGroup != null)
        {
            overlayImage.color = fadeColor;
            overlayCanvas.sortingOrder = sortingOrder;
            return;
        }

        GameObject canvasObject = new("ScreenFadeOverlayCanvas");
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = sortingOrder;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new("FadeImage");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        overlayImage = imageObject.AddComponent<Image>();
        overlayImage.color = fadeColor;

        overlayCanvasGroup = imageObject.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;
    }
}