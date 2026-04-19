using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIFadeOverlayFix : MonoBehaviour
{
    private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Image image;
    private Material mat;
    private Material sourceMaterial;
    private bool ownsRuntimeMaterial;

    void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        image = GetComponent<Image>();
        
        if (image != null)
        {
            sourceMaterial = image.material;

            // UI Images do not expose sharedMaterial. Clone the assigned material at runtime
            // so shader property updates stay local to this overlay.
            if (Application.isPlaying && sourceMaterial != null)
            {
                mat = new Material(sourceMaterial);
                mat.name = sourceMaterial.name + " (UIFadeOverlayFix)";
                image.material = mat;
                ownsRuntimeMaterial = true;
                image.SetMaterialDirty();
            }
            else
            {
                mat = sourceMaterial;
                ownsRuntimeMaterial = false;
            }
        }
    }

    void OnDisable()
    {
        if (image != null && ownsRuntimeMaterial)
        {
            image.material = sourceMaterial;
        }

        if (ownsRuntimeMaterial && mat != null)
        {
            Destroy(mat);
        }

        mat = null;
        sourceMaterial = null;
        ownsRuntimeMaterial = false;
    }

    void Update()
    {
        if (rectTransform != null && mat != null && Screen.width > 0)
        {
            if (mat.HasProperty(MainTexId) && image != null && image.mainTexture != null)
            {
                // Runtime material instances can lose the sprite binding on UI shaders.
                // Push the active UI texture explicitly so the graph samples the right sprite.
                mat.SetTexture(MainTexId, image.mainTexture);
            }

            // Convert the UI corners into real screen pixels so they match Shader Graph
            // Screen Position in Game view for overlay and camera-space canvases.
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Camera uiCamera = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = parentCanvas.worldCamera;
            }

            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

            // Normalize the horizontal bounds so the shader receives 0..1 values.
            float normalizedStart = Mathf.Clamp01(bottomLeft.x / Screen.width);
            float normalizedEnd = Mathf.Clamp01(topRight.x / Screen.width);

            mat.SetFloat(FadeStartId, normalizedStart);
            mat.SetFloat(FadeEndId, normalizedEnd);
        }
    }
}