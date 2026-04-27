using System;
using System.Collections.Generic;
using UnityEngine;

public class DistanceMaterialTransparencyController : MonoBehaviour
{
    private static readonly int ToonTransparencyProperty = Shader.PropertyToID(
        "_Tweak_transparency");
    private static readonly int ToonTransparentEnabledProperty = Shader.PropertyToID(
        "_TransparentEnabled");
    private static readonly int ToonClippingModeProperty = Shader.PropertyToID(
        "_ClippingMode");
    private static readonly int ToonSrcBlendProperty = Shader.PropertyToID("_SrcBlend");
    private static readonly int ToonDstBlendProperty = Shader.PropertyToID("_DstBlend");
    private static readonly int ToonAlphaSrcBlendProperty = Shader.PropertyToID(
        "_AlphaSrcBlend");
    private static readonly int ToonAlphaDstBlendProperty = Shader.PropertyToID(
        "_AlphaDstBlend");
    private static readonly int ToonZWriteProperty = Shader.PropertyToID("_TransparentZWrite");
    private static readonly int ToonOpaqueClippingMode = 0;
    private static readonly int ToonTransparentClippingMode = 2;
    private static readonly int LitSurfaceProperty = Shader.PropertyToID("_Surface");
    private static readonly int LitBaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int LitBlendProperty = Shader.PropertyToID("_Blend");
    private static readonly int LitSrcBlendProperty = Shader.PropertyToID("_SrcBlend");
    private static readonly int LitDstBlendProperty = Shader.PropertyToID("_DstBlend");
    private static readonly int LitSrcBlendAlphaProperty = Shader.PropertyToID("_SrcBlendAlpha");
    private static readonly int LitDstBlendAlphaProperty = Shader.PropertyToID("_DstBlendAlpha");
    private static readonly int LitZWriteProperty = Shader.PropertyToID("_ZWrite");
    private static readonly int SurfaceOpaque = 0;
    private static readonly int SurfaceTransparent = 1;

    [Header("Distance Targets")]

    [SerializeField] private GameObject firstTarget;
    [SerializeField] private GameObject secondTarget;

    [Header("Toon Materials")]

    [SerializeField] private List<Renderer> toonMeshes = new();
    [SerializeField] private List<Material> toonMaterialAssets = new();
    [SerializeField] private float restoredToonTransparency = 0f;
    [SerializeField] private float hiddenToonTransparency = -0.99f;
    [SerializeField, Range(0.01f, 0.1f)] private float toonTransparencyStep = 0.01f;

    [Header("URP Lit Materials")]

    [SerializeField] private List<Renderer> litMeshes = new();
    [SerializeField] private List<Material> litMaterialAssets = new();
    [SerializeField, Range(0f, 1f)] private float restoredLitAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float hiddenLitAlpha = 0f;

    [Header("Distance Thresholds")]

    [SerializeField, Min(0f)] private float immediateHideDistance = 1.5f;
    [SerializeField, Min(0f)] private float fadeStartDistance = 3f;

    [Header("Update Settings")]

    [SerializeField, Min(0f)] private float transparencyUpdateInterval = 0.02f;
    [SerializeField] private bool debugDistanceReadout = false;
    [SerializeField, Min(0.05f)] private float debugLogInterval = 0.25f;

    private readonly List<ToonMaterialState> toonMaterials = new();
    private readonly List<LitMaterialState> litMaterials = new();
    private float nextTransparencyUpdateTime;
    private float nextDebugLogTime;

    private void Awake()
    {
        CacheMaterials();
        ApplyCurrentDistance();
    }

    private void OnEnable()
    {
        nextTransparencyUpdateTime = 0f;
        nextDebugLogTime = 0f;
        ApplyCurrentDistance();
    }

    private void Update()
    {
        if (transparencyUpdateInterval > 0f && Time.time < nextTransparencyUpdateTime)
        {
            return;
        }

        nextTransparencyUpdateTime = Time.time + transparencyUpdateInterval;
        ApplyCurrentDistance();
    }

    private void OnDisable()
    {
        RestoreMaterials();
    }

    private void OnValidate()
    {
        if (fadeStartDistance < immediateHideDistance)
        {
            fadeStartDistance = immediateHideDistance;
        }

        restoredToonTransparency = Mathf.Clamp(restoredToonTransparency, -0.99f, 0f);
        hiddenToonTransparency = Mathf.Clamp(hiddenToonTransparency, -0.99f, 0f);
        toonTransparencyStep = Mathf.Clamp(toonTransparencyStep, 0.01f, 0.1f);
        transparencyUpdateInterval = Mathf.Max(0f, transparencyUpdateInterval);
        debugLogInterval = Mathf.Max(0.05f, debugLogInterval);
    }

    private void CacheMaterials()
    {
        toonMaterials.Clear();
        litMaterials.Clear();

        HashSet<Material> toonUniqueMaterials = new();
        HashSet<Material> litUniqueMaterials = new();

        CacheToonMaterials(toonUniqueMaterials);
        CacheLitMaterials(litUniqueMaterials);
    }

    private void CacheToonMaterials(HashSet<Material> uniqueMaterials)
    {
        foreach (Material material in toonMaterialAssets)
        {
            RegisterToonMaterial(material, uniqueMaterials);
        }

        foreach (Renderer meshRenderer in toonMeshes)
        {
            if (meshRenderer == null)
            {
                continue;
            }

            foreach (Material material in meshRenderer.sharedMaterials)
            {
                RegisterToonMaterial(material, uniqueMaterials);
            }
        }
    }

    private void CacheLitMaterials(HashSet<Material> uniqueMaterials)
    {
        foreach (Material material in litMaterialAssets)
        {
            RegisterLitMaterial(material, uniqueMaterials);
        }

        foreach (Renderer meshRenderer in litMeshes)
        {
            if (meshRenderer == null)
            {
                continue;
            }

            foreach (Material material in meshRenderer.sharedMaterials)
            {
                RegisterLitMaterial(material, uniqueMaterials);
            }
        }
    }

    private void RegisterToonMaterial(Material material, HashSet<Material> uniqueMaterials)
    {
        if (material == null || !uniqueMaterials.Add(material))
        {
            return;
        }

        float originalTransparency = material.HasProperty(ToonTransparencyProperty)
            ? material.GetFloat(ToonTransparencyProperty)
            : restoredToonTransparency;
        float originalTransparentEnabled = material.HasProperty(ToonTransparentEnabledProperty)
            ? material.GetFloat(ToonTransparentEnabledProperty)
            : 0f;
        float originalClippingMode = material.HasProperty(ToonClippingModeProperty)
            ? material.GetFloat(ToonClippingModeProperty)
            : ToonOpaqueClippingMode;
        float originalSrcBlend = material.HasProperty(ToonSrcBlendProperty)
            ? material.GetFloat(ToonSrcBlendProperty)
            : (float)UnityEngine.Rendering.BlendMode.One;
        float originalDstBlend = material.HasProperty(ToonDstBlendProperty)
            ? material.GetFloat(ToonDstBlendProperty)
            : (float)UnityEngine.Rendering.BlendMode.Zero;
        float originalAlphaSrcBlend = material.HasProperty(ToonAlphaSrcBlendProperty)
            ? material.GetFloat(ToonAlphaSrcBlendProperty)
            : (float)UnityEngine.Rendering.BlendMode.One;
        float originalAlphaDstBlend = material.HasProperty(ToonAlphaDstBlendProperty)
            ? material.GetFloat(ToonAlphaDstBlendProperty)
            : (float)UnityEngine.Rendering.BlendMode.Zero;
        float originalZWrite = material.HasProperty(ToonZWriteProperty)
            ? material.GetFloat(ToonZWriteProperty)
            : 0f;

        toonMaterials.Add(
            new ToonMaterialState(
                material,
                originalTransparency,
                originalTransparentEnabled,
                originalClippingMode,
                originalSrcBlend,
                originalDstBlend,
                originalAlphaSrcBlend,
                originalAlphaDstBlend,
                originalZWrite,
                material.renderQueue,
                material.GetTag("RenderType", false, "Opaque")
            )
        );
    }

    private void RegisterLitMaterial(Material material, HashSet<Material> uniqueMaterials)
    {
        if (material == null || !uniqueMaterials.Add(material))
        {
            return;
        }

        Color originalBaseColor = material.HasProperty(LitBaseColorProperty)
            ? material.GetColor(LitBaseColorProperty)
            : Color.white;

        litMaterials.Add(
            new LitMaterialState(
                material,
                originalBaseColor,
                material.HasProperty(LitSurfaceProperty)
                    ? material.GetFloat(LitSurfaceProperty)
                    : SurfaceOpaque,
                material.HasProperty(LitBlendProperty)
                    ? material.GetFloat(LitBlendProperty)
                    : 0f,
                material.HasProperty(LitSrcBlendProperty)
                    ? material.GetFloat(LitSrcBlendProperty)
                    : (float)UnityEngine.Rendering.BlendMode.One,
                material.HasProperty(LitDstBlendProperty)
                    ? material.GetFloat(LitDstBlendProperty)
                    : (float)UnityEngine.Rendering.BlendMode.Zero,
                material.HasProperty(LitSrcBlendAlphaProperty)
                    ? material.GetFloat(LitSrcBlendAlphaProperty)
                    : (float)UnityEngine.Rendering.BlendMode.One,
                material.HasProperty(LitDstBlendAlphaProperty)
                    ? material.GetFloat(LitDstBlendAlphaProperty)
                    : (float)UnityEngine.Rendering.BlendMode.Zero,
                material.HasProperty(LitZWriteProperty)
                    ? material.GetFloat(LitZWriteProperty)
                    : 1f,
                material.renderQueue
            )
        );
    }

    private void ApplyCurrentDistance()
    {
        if (firstTarget == null || secondTarget == null)
        {
            RestoreMaterials();
            return;
        }

        if (toonMaterials.Count == 0 && litMaterials.Count == 0)
        {
            CacheMaterials();
        }

        float distance = Vector3.Distance(
            firstTarget.transform.position,
            secondTarget.transform.position);
        float fadeProgress = GetFadeProgress(distance);
        float toonFadeProgress = GetToonFadeProgress(distance);
        bool enableToonTransparency = distance <= fadeStartDistance;
        float toonTransparencyValue = GetQuantizedToonTransparency(toonFadeProgress);
        float litAlphaValue = Mathf.Lerp(restoredLitAlpha, hiddenLitAlpha, fadeProgress);

        ApplyToonFade(toonTransparencyValue, enableToonTransparency);
        ApplyLitFade(litAlphaValue, fadeProgress);
        MaybeLogDebugState(
            distance,
            toonFadeProgress,
            fadeProgress,
            toonTransparencyValue,
            litAlphaValue,
            enableToonTransparency);
    }

    private float GetFadeProgress(float distance)
    {
        if (distance <= immediateHideDistance)
        {
            return 1f;
        }

        if (distance >= fadeStartDistance)
        {
            return 0f;
        }

        float fadeRange = fadeStartDistance - immediateHideDistance;
        if (fadeRange <= Mathf.Epsilon)
        {
            return 1f;
        }

        return 1f - Mathf.InverseLerp(immediateHideDistance, fadeStartDistance, distance);
    }

    private float GetToonFadeProgress(float distance)
    {
        if (distance <= immediateHideDistance)
        {
            return 1f;
        }

        if (distance >= fadeStartDistance)
        {
            return 0f;
        }

        float fadeRange = fadeStartDistance - immediateHideDistance;
        if (fadeRange <= Mathf.Epsilon)
        {
            return 1f;
        }

        return 1f - Mathf.InverseLerp(immediateHideDistance, fadeStartDistance, distance);
    }

    private void ApplyToonFade(float transparencyValue, bool enableTransparency)
    {
        foreach (ToonMaterialState state in toonMaterials)
        {
            if (state.Material == null)
            {
                continue;
            }

            if (state.Material.HasProperty(ToonTransparencyProperty))
            {
                state.Material.SetFloat(ToonTransparencyProperty, transparencyValue);
            }

            if (enableTransparency)
            {
                SetToonTransparent(state.Material);
                continue;
            }

            RestoreToonMaterial(state);
        }
    }

    private void ApplyLitFade(float alphaValue, float fadeProgress)
    {
        foreach (LitMaterialState state in litMaterials)
        {
            if (state.Material == null)
            {
                continue;
            }

            Color color = state.OriginalBaseColor;
            color.a = alphaValue;

            if (state.Material.HasProperty(LitBaseColorProperty))
            {
                state.Material.SetColor(LitBaseColorProperty, color);
            }

            if (fadeProgress > 0f)
            {
                SetLitTransparent(state.Material);
                continue;
            }

            RestoreLitMaterial(state);
        }
    }

    private void RestoreMaterials()
    {
        foreach (ToonMaterialState state in toonMaterials)
        {
            if (state.Material == null)
            {
                continue;
            }

            if (state.Material.HasProperty(ToonTransparencyProperty))
            {
                state.Material.SetFloat(ToonTransparencyProperty, state.OriginalTransparency);
            }

            RestoreToonMaterial(state);
        }

        foreach (LitMaterialState state in litMaterials)
        {
            if (state.Material == null)
            {
                continue;
            }

            RestoreLitMaterial(state);
        }
    }

    private static void SetLitTransparent(Material material)
    {
        if (material.HasProperty(LitSurfaceProperty))
        {
            material.SetFloat(LitSurfaceProperty, SurfaceTransparent);
        }

        if (material.HasProperty(LitBlendProperty))
        {
            material.SetFloat(LitBlendProperty, 0f);
        }

        if (material.HasProperty(LitSrcBlendProperty))
        {
            material.SetFloat(LitSrcBlendProperty, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty(LitDstBlendProperty))
        {
            material.SetFloat(
                LitDstBlendProperty,
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(LitSrcBlendAlphaProperty))
        {
            material.SetFloat(LitSrcBlendAlphaProperty, (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (material.HasProperty(LitDstBlendAlphaProperty))
        {
            material.SetFloat(
                LitDstBlendAlphaProperty,
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(LitZWriteProperty))
        {
            material.SetFloat(LitZWriteProperty, 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
    }

    private static void SetToonTransparent(Material material)
    {
        if (material.HasProperty(ToonTransparentEnabledProperty))
        {
            material.SetFloat(ToonTransparentEnabledProperty, 1f);
        }

        if (material.HasProperty(ToonClippingModeProperty))
        {
            material.SetFloat(ToonClippingModeProperty, ToonTransparentClippingMode);
        }

        if (material.HasProperty(ToonSrcBlendProperty))
        {
            material.SetFloat(
                ToonSrcBlendProperty,
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty(ToonDstBlendProperty))
        {
            material.SetFloat(
                ToonDstBlendProperty,
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ToonAlphaSrcBlendProperty))
        {
            material.SetFloat(
                ToonAlphaSrcBlendProperty,
                (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (material.HasProperty(ToonAlphaDstBlendProperty))
        {
            material.SetFloat(
                ToonAlphaDstBlendProperty,
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ToonZWriteProperty))
        {
            material.SetFloat(ToonZWriteProperty, 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private float GetQuantizedToonTransparency(float fadeProgress)
    {
        float transparencyValue = Mathf.Lerp(
            restoredToonTransparency,
            hiddenToonTransparency,
            fadeProgress);
        return QuantizeToonTransparency(transparencyValue);
    }

    private float QuantizeToonTransparency(float transparencyValue)
    {
        float stepCount = Mathf.Round(transparencyValue / toonTransparencyStep);
        float quantizedValue = stepCount * toonTransparencyStep;
        return Mathf.Clamp(quantizedValue, hiddenToonTransparency, restoredToonTransparency);
    }

    private void MaybeLogDebugState(
        float distance,
        float toonFadeProgress,
        float litFadeProgress,
        float toonTransparencyValue,
        float litAlphaValue,
        bool toonTransparencyEnabled)
    {
        if (!debugDistanceReadout)
        {
            return;
        }

        if (Application.isPlaying && Time.time < nextDebugLogTime)
        {
            return;
        }

        if (Application.isPlaying)
        {
            nextDebugLogTime = Time.time + debugLogInterval;
        }

        Debug.Log(
            $"[DistanceMaterialTransparencyController] Distance={distance:F3} | " +
            $"ToonFade={toonFadeProgress:F3} | ToonValue={toonTransparencyValue:F2} | " +
            $"ToonTransparent={(toonTransparencyEnabled ? 1 : 0)} | " +
            $"LitFade={litFadeProgress:F3} | LitAlpha={litAlphaValue:F2} | " +
            $"UpdateInterval={transparencyUpdateInterval:F3}s",
            this);
    }

    private static void RestoreToonMaterial(ToonMaterialState state)
    {
        if (state.Material.HasProperty(ToonTransparentEnabledProperty))
        {
            state.Material.SetFloat(
                ToonTransparentEnabledProperty,
                state.OriginalTransparentEnabled);
        }

        if (state.Material.HasProperty(ToonClippingModeProperty))
        {
            state.Material.SetFloat(ToonClippingModeProperty, state.OriginalClippingMode);
        }

        if (state.Material.HasProperty(ToonSrcBlendProperty))
        {
            state.Material.SetFloat(ToonSrcBlendProperty, state.OriginalSrcBlend);
        }

        if (state.Material.HasProperty(ToonDstBlendProperty))
        {
            state.Material.SetFloat(ToonDstBlendProperty, state.OriginalDstBlend);
        }

        if (state.Material.HasProperty(ToonAlphaSrcBlendProperty))
        {
            state.Material.SetFloat(ToonAlphaSrcBlendProperty, state.OriginalAlphaSrcBlend);
        }

        if (state.Material.HasProperty(ToonAlphaDstBlendProperty))
        {
            state.Material.SetFloat(ToonAlphaDstBlendProperty, state.OriginalAlphaDstBlend);
        }

        if (state.Material.HasProperty(ToonZWriteProperty))
        {
            state.Material.SetFloat(ToonZWriteProperty, state.OriginalZWrite);
        }

        state.Material.SetOverrideTag("RenderType", state.OriginalRenderType);
        state.Material.renderQueue = state.OriginalRenderQueue;
    }

    private static void RestoreLitMaterial(LitMaterialState state)
    {
        if (state.Material.HasProperty(LitBaseColorProperty))
        {
            state.Material.SetColor(LitBaseColorProperty, state.OriginalBaseColor);
        }

        if (state.Material.HasProperty(LitSurfaceProperty))
        {
            state.Material.SetFloat(LitSurfaceProperty, state.OriginalSurface);
        }

        if (state.Material.HasProperty(LitBlendProperty))
        {
            state.Material.SetFloat(LitBlendProperty, state.OriginalBlend);
        }

        if (state.Material.HasProperty(LitSrcBlendProperty))
        {
            state.Material.SetFloat(LitSrcBlendProperty, state.OriginalSrcBlend);
        }

        if (state.Material.HasProperty(LitDstBlendProperty))
        {
            state.Material.SetFloat(LitDstBlendProperty, state.OriginalDstBlend);
        }

        if (state.Material.HasProperty(LitSrcBlendAlphaProperty))
        {
            state.Material.SetFloat(LitSrcBlendAlphaProperty, state.OriginalSrcBlendAlpha);
        }

        if (state.Material.HasProperty(LitDstBlendAlphaProperty))
        {
            state.Material.SetFloat(LitDstBlendAlphaProperty, state.OriginalDstBlendAlpha);
        }

        if (state.Material.HasProperty(LitZWriteProperty))
        {
            state.Material.SetFloat(LitZWriteProperty, state.OriginalZWrite);
        }

        state.Material.SetOverrideTag(
            "RenderType",
            state.OriginalSurface >= SurfaceTransparent ? "Transparent" : "Opaque");
        state.Material.renderQueue = state.OriginalRenderQueue;

        if (state.OriginalSurface >= SurfaceTransparent)
        {
            state.Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            state.Material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            return;
        }

        state.Material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
        state.Material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    [Serializable]
    private sealed class ToonMaterialState
    {
        public ToonMaterialState(
            Material material,
            float originalTransparency,
            float originalTransparentEnabled,
            float originalClippingMode,
            float originalSrcBlend,
            float originalDstBlend,
            float originalAlphaSrcBlend,
            float originalAlphaDstBlend,
            float originalZWrite,
            int originalRenderQueue,
            string originalRenderType
        )
        {
            Material = material;
            OriginalTransparency = originalTransparency;
            OriginalTransparentEnabled = originalTransparentEnabled;
            OriginalClippingMode = originalClippingMode;
            OriginalSrcBlend = originalSrcBlend;
            OriginalDstBlend = originalDstBlend;
            OriginalAlphaSrcBlend = originalAlphaSrcBlend;
            OriginalAlphaDstBlend = originalAlphaDstBlend;
            OriginalZWrite = originalZWrite;
            OriginalRenderQueue = originalRenderQueue;
            OriginalRenderType = originalRenderType;
        }

        public Material Material { get; }
        public float OriginalTransparency { get; }
        public float OriginalTransparentEnabled { get; }
        public float OriginalClippingMode { get; }
        public float OriginalSrcBlend { get; }
        public float OriginalDstBlend { get; }
        public float OriginalAlphaSrcBlend { get; }
        public float OriginalAlphaDstBlend { get; }
        public float OriginalZWrite { get; }
        public int OriginalRenderQueue { get; }
        public string OriginalRenderType { get; }
    }

    [Serializable]
    private sealed class LitMaterialState
    {
        public LitMaterialState(
            Material material,
            Color originalBaseColor,
            float originalSurface,
            float originalBlend,
            float originalSrcBlend,
            float originalDstBlend,
            float originalSrcBlendAlpha,
            float originalDstBlendAlpha,
            float originalZWrite,
            int originalRenderQueue
        )
        {
            Material = material;
            OriginalBaseColor = originalBaseColor;
            OriginalSurface = originalSurface;
            OriginalBlend = originalBlend;
            OriginalSrcBlend = originalSrcBlend;
            OriginalDstBlend = originalDstBlend;
            OriginalSrcBlendAlpha = originalSrcBlendAlpha;
            OriginalDstBlendAlpha = originalDstBlendAlpha;
            OriginalZWrite = originalZWrite;
            OriginalRenderQueue = originalRenderQueue;
        }

        public Material Material { get; }
        public Color OriginalBaseColor { get; }
        public float OriginalSurface { get; }
        public float OriginalBlend { get; }
        public float OriginalSrcBlend { get; }
        public float OriginalDstBlend { get; }
        public float OriginalSrcBlendAlpha { get; }
        public float OriginalDstBlendAlpha { get; }
        public float OriginalZWrite { get; }
        public int OriginalRenderQueue { get; }
    }
}