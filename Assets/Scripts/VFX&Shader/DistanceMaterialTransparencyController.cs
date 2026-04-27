using System;
using System.Collections.Generic;
using UnityEngine;

public class DistanceMaterialTransparencyController : MonoBehaviour
{
    private static readonly int ToonTransparencyProperty = Shader.PropertyToID("_Tweak_transparency");
    private static readonly int ToonTransparentEnabledProperty = Shader.PropertyToID("_TransparentEnabled");
    private static readonly int ToonClippingModeProperty = Shader.PropertyToID("_ClippingMode");
    private static readonly int ToonZWriteModeProperty = Shader.PropertyToID("_ZWriteMode");
    private static readonly int ToonZOverDrawModeProperty = Shader.PropertyToID("_ZOverDrawMode");
    private static readonly int ToonOutlineColorMaskProperty = Shader.PropertyToID("_SPRDefaultUnlitColorMask");
    private static readonly int ToonOutlineCullModeProperty = Shader.PropertyToID("_SRPDefaultUnlitColMode");
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
    private const int ToonClippingOff = 0;
    private const int ToonClippingOn = 1;
    private const int ToonTransClipping = 2;
    private const int ToonOutlineCullFront = 1;
    private const int ToonOutlineCullBack = 2;
    private const string ToonKeywordClippingOff = "_IS_CLIPPING_OFF";
    private const string ToonKeywordClippingMode = "_IS_CLIPPING_MODE";
    private const string ToonKeywordClippingTransparent = "_IS_CLIPPING_TRANSMODE";
    private const string ToonKeywordOutlineClippingNo = "_IS_OUTLINE_CLIPPING_NO";
    private const string ToonKeywordOutlineClippingYes = "_IS_OUTLINE_CLIPPING_YES";
    private const string RenderTypeTag = "RenderType";
    private const string IgnoreProjectionTag = "IgnoreProjection";
    private const string RenderTypeOpaque = "Opaque";
    private const string RenderTypeTransparentCutout = "TransparentCutOut";
    private const string RenderTypeTransparent = "Transparent";

    [Header("Distance Targets")]
    [SerializeField] private GameObject firstTarget;
    [SerializeField] private GameObject secondTarget;

    [Header("Toon Materials")]
    [SerializeField] private List<Renderer> toonMeshes = new();
    [SerializeField] private List<Material> toonMaterialAssets = new();
    [SerializeField] private float restoredToonTransparency = 0f;
    [SerializeField] private float hiddenToonTransparency = -1f;

    [Header("URP Lit Materials")]
    [SerializeField] private List<Renderer> litMeshes = new();
    [SerializeField] private List<Material> litMaterialAssets = new();
    [SerializeField, Range(0f, 1f)] private float restoredLitAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float hiddenLitAlpha = 0f;

    [Header("Distance Thresholds")]
    [SerializeField, Min(0f)] private float immediateHideDistance = 1.5f;
    [SerializeField, Min(0f)] private float fadeStartDistance = 3f;

    private readonly List<ToonMaterialState> toonMaterials = new();
    private readonly List<LitMaterialState> litMaterials = new();

    private void Awake()
    {
        CacheMaterials();
        ApplyCurrentDistance();
    }

    private void OnEnable()
    {
        ApplyCurrentDistance();
    }

    private void Update()
    {
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
            : ToonClippingOff;
        float originalZWriteMode = material.HasProperty(ToonZWriteModeProperty)
            ? material.GetFloat(ToonZWriteModeProperty)
            : 1f;
        float originalZOverDrawMode = material.HasProperty(ToonZOverDrawModeProperty)
            ? material.GetFloat(ToonZOverDrawModeProperty)
            : 0f;
        float originalOutlineColorMask = material.HasProperty(ToonOutlineColorMaskProperty)
            ? material.GetFloat(ToonOutlineColorMaskProperty)
            : 15f;
        float originalOutlineCullMode = material.HasProperty(ToonOutlineCullModeProperty)
            ? material.GetFloat(ToonOutlineCullModeProperty)
            : ToonOutlineCullFront;

        toonMaterials.Add(new ToonMaterialState(
            material,
            originalTransparency,
            originalTransparentEnabled,
            originalClippingMode,
            originalZWriteMode,
            originalZOverDrawMode,
            originalOutlineColorMask,
            originalOutlineCullMode,
            material.GetTag(RenderTypeTag, false, RenderTypeOpaque),
            material.GetTag(IgnoreProjectionTag, false, "False"),
            material.renderQueue));
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

        litMaterials.Add(new LitMaterialState(
            material,
            originalBaseColor,
            material.HasProperty(LitSurfaceProperty) ? material.GetFloat(LitSurfaceProperty) : SurfaceOpaque,
            material.HasProperty(LitBlendProperty) ? material.GetFloat(LitBlendProperty) : 0f,
            material.HasProperty(LitSrcBlendProperty) ? material.GetFloat(LitSrcBlendProperty) : (float)UnityEngine.Rendering.BlendMode.One,
            material.HasProperty(LitDstBlendProperty) ? material.GetFloat(LitDstBlendProperty) : (float)UnityEngine.Rendering.BlendMode.Zero,
            material.HasProperty(LitSrcBlendAlphaProperty) ? material.GetFloat(LitSrcBlendAlphaProperty) : (float)UnityEngine.Rendering.BlendMode.One,
            material.HasProperty(LitDstBlendAlphaProperty) ? material.GetFloat(LitDstBlendAlphaProperty) : (float)UnityEngine.Rendering.BlendMode.Zero,
            material.HasProperty(LitZWriteProperty) ? material.GetFloat(LitZWriteProperty) : 1f,
            material.renderQueue));
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

        float distance = Vector3.Distance(firstTarget.transform.position, secondTarget.transform.position);
        float fadeProgress = GetFadeProgress(distance);

        ApplyToonFade(fadeProgress);
        ApplyLitFade(fadeProgress);
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

    private void ApplyToonFade(float fadeProgress)
    {
        float transparencyValue = Mathf.Lerp(restoredToonTransparency, hiddenToonTransparency, fadeProgress);

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

            if (fadeProgress > 0f)
            {
                SetToonTransparent(state.Material);
                continue;
            }

            RestoreToonMaterial(state);
        }
    }

    private void ApplyLitFade(float fadeProgress)
    {
        foreach (LitMaterialState state in litMaterials)
        {
            if (state.Material == null)
            {
                continue;
            }

            float alpha = Mathf.Lerp(restoredLitAlpha, hiddenLitAlpha, fadeProgress);
            Color color = state.OriginalBaseColor;
            color.a = alpha;

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
            material.SetFloat(LitDstBlendProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(LitSrcBlendAlphaProperty))
        {
            material.SetFloat(LitSrcBlendAlphaProperty, (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (material.HasProperty(LitDstBlendAlphaProperty))
        {
            material.SetFloat(LitDstBlendAlphaProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
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
            material.SetFloat(ToonClippingModeProperty, ToonTransClipping);
        }

        if (material.HasProperty(ToonZWriteModeProperty))
        {
            material.SetFloat(ToonZWriteModeProperty, 0f);
        }

        if (material.HasProperty(ToonZOverDrawModeProperty))
        {
            material.SetFloat(ToonZOverDrawModeProperty, 1f);
        }

        if (material.HasProperty(ToonOutlineColorMaskProperty))
        {
            material.SetFloat(ToonOutlineColorMaskProperty, 0f);
        }

        if (material.HasProperty(ToonOutlineCullModeProperty))
        {
            material.SetFloat(ToonOutlineCullModeProperty, ToonOutlineCullBack);
        }

        material.DisableKeyword(ToonKeywordClippingOff);
        material.DisableKeyword(ToonKeywordClippingMode);
        material.EnableKeyword(ToonKeywordClippingTransparent);
        material.DisableKeyword(ToonKeywordOutlineClippingNo);
        material.EnableKeyword(ToonKeywordOutlineClippingYes);
        material.SetOverrideTag(RenderTypeTag, RenderTypeTransparent);
        material.SetOverrideTag(IgnoreProjectionTag, "True");
        material.renderQueue = 3000;
    }

    private static void RestoreToonMaterial(ToonMaterialState state)
    {
        if (state.Material.HasProperty(ToonTransparencyProperty))
        {
            state.Material.SetFloat(ToonTransparencyProperty, state.OriginalTransparency);
        }

        if (state.Material.HasProperty(ToonTransparentEnabledProperty))
        {
            state.Material.SetFloat(ToonTransparentEnabledProperty, state.OriginalTransparentEnabled);
        }

        if (state.Material.HasProperty(ToonClippingModeProperty))
        {
            state.Material.SetFloat(ToonClippingModeProperty, state.OriginalClippingMode);
        }

        if (state.Material.HasProperty(ToonZWriteModeProperty))
        {
            state.Material.SetFloat(ToonZWriteModeProperty, state.OriginalZWriteMode);
        }

        if (state.Material.HasProperty(ToonZOverDrawModeProperty))
        {
            state.Material.SetFloat(ToonZOverDrawModeProperty, state.OriginalZOverDrawMode);
        }

        if (state.Material.HasProperty(ToonOutlineColorMaskProperty))
        {
            state.Material.SetFloat(ToonOutlineColorMaskProperty, state.OriginalOutlineColorMask);
        }

        if (state.Material.HasProperty(ToonOutlineCullModeProperty))
        {
            state.Material.SetFloat(ToonOutlineCullModeProperty, state.OriginalOutlineCullMode);
        }

        ApplyToonClippingKeywords(state.Material, state.OriginalClippingMode);
        state.Material.SetOverrideTag(RenderTypeTag, state.OriginalRenderTypeTag);
        state.Material.SetOverrideTag(IgnoreProjectionTag, state.OriginalIgnoreProjectionTag);
        state.Material.renderQueue = state.OriginalRenderQueue;
    }

    private static void ApplyToonClippingKeywords(Material material, float clippingMode)
    {
        int clippingModeInt = Mathf.RoundToInt(clippingMode);

        switch (clippingModeInt)
        {
            case ToonClippingOff:
                material.EnableKeyword(ToonKeywordClippingOff);
                material.DisableKeyword(ToonKeywordClippingMode);
                material.DisableKeyword(ToonKeywordClippingTransparent);
                material.EnableKeyword(ToonKeywordOutlineClippingNo);
                material.DisableKeyword(ToonKeywordOutlineClippingYes);
                break;
            case ToonClippingOn:
                material.DisableKeyword(ToonKeywordClippingOff);
                material.EnableKeyword(ToonKeywordClippingMode);
                material.DisableKeyword(ToonKeywordClippingTransparent);
                material.DisableKeyword(ToonKeywordOutlineClippingNo);
                material.EnableKeyword(ToonKeywordOutlineClippingYes);
                break;
            default:
                material.DisableKeyword(ToonKeywordClippingOff);
                material.DisableKeyword(ToonKeywordClippingMode);
                material.EnableKeyword(ToonKeywordClippingTransparent);
                material.DisableKeyword(ToonKeywordOutlineClippingNo);
                material.EnableKeyword(ToonKeywordOutlineClippingYes);
                break;
        }
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

        state.Material.SetOverrideTag("RenderType", state.OriginalSurface >= SurfaceTransparent ? "Transparent" : "Opaque");
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
            float originalZWriteMode,
            float originalZOverDrawMode,
            float originalOutlineColorMask,
            float originalOutlineCullMode,
            string originalRenderTypeTag,
            string originalIgnoreProjectionTag,
            int originalRenderQueue)
        {
            Material = material;
            OriginalTransparency = originalTransparency;
            OriginalTransparentEnabled = originalTransparentEnabled;
            OriginalClippingMode = originalClippingMode;
            OriginalZWriteMode = originalZWriteMode;
            OriginalZOverDrawMode = originalZOverDrawMode;
            OriginalOutlineColorMask = originalOutlineColorMask;
            OriginalOutlineCullMode = originalOutlineCullMode;
            OriginalRenderTypeTag = originalRenderTypeTag;
            OriginalIgnoreProjectionTag = originalIgnoreProjectionTag;
            OriginalRenderQueue = originalRenderQueue;
        }

        public Material Material { get; }
        public float OriginalTransparency { get; }
        public float OriginalTransparentEnabled { get; }
        public float OriginalClippingMode { get; }
        public float OriginalZWriteMode { get; }
        public float OriginalZOverDrawMode { get; }
        public float OriginalOutlineColorMask { get; }
        public float OriginalOutlineCullMode { get; }
        public string OriginalRenderTypeTag { get; }
        public string OriginalIgnoreProjectionTag { get; }
        public int OriginalRenderQueue { get; }
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
            int originalRenderQueue)
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