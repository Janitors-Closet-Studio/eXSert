// Ignore Spelling: Gizmo

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    /// <summary>
    /// This saves all user settings for Better Mesh Asset
    /// </summary>
#if BETTEREDITOR_USE_PROJECTSETTINGS
    [FilePath("ProjectSettings/Tiny Giant Studio/Better Editor/BetterMesh Settings.asset", FilePathAttribute.Location.ProjectFolder)]
#else
    [FilePath("UserSettings/Tiny Giant Studio/Better Editor/BetterMesh Settings.asset", FilePathAttribute.Location.ProjectFolder)]
#endif
    public class BetterMeshSettings : ScriptableSingleton<BetterMeshSettings>
    {
        public bool meshFieldOnTop = true;

        [FormerlySerializedAs("_selectedUnit")] [SerializeField]
        int selectedUnit;

        public int SelectedUnit
        {
            get => selectedUnit;
            set
            {
                selectedUnit = value;
                Save(true);
            }
        }

        #region Inspector Customization

        [FormerlySerializedAs("_overrideInspectorColor")] [SerializeField]
        bool overrideInspectorColor;

        public bool OverrideInspectorColor
        {
            get => overrideInspectorColor;
            set
            {
                overrideInspectorColor = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_inspectorColor")] [SerializeField]
        Color inspectorColor = new(0, 0, 1, 0.025f);

        public Color InspectorColor
        {
            get => inspectorColor;
            set
            {
                inspectorColor = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_overrideFoldoutColor")] [SerializeField]
        bool overrideFoldoutColor;

        public bool OverrideFoldoutColor
        {
            get => overrideFoldoutColor;
            set
            {
                overrideFoldoutColor = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_foldoutColor")] [SerializeField]
        Color foldoutColor = new(0, 1, 0, 0.025f);

        public Color FoldoutColor
        {
            get => foldoutColor;
            set
            {
                foldoutColor = value;
                Save(true);
            }
        }

        #endregion Inspector Customization

        #region Mesh Preview

        [FormerlySerializedAs("_showMeshPreview")] [SerializeField]
        bool showMeshPreview = true;

        public bool ShowMeshPreview
        {
            get => showMeshPreview;
            set
            {
                showMeshPreview = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_meshPreviewHeight")] [SerializeField]
        float meshPreviewHeight = 200;

        public float MeshPreviewHeight
        {
            get => meshPreviewHeight;
            set
            {
                meshPreviewHeight = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_maxPreviewCount")] [SerializeField]
        int maxPreviewCount = 20;

        public int MaxPreviewCount
        {
            get => maxPreviewCount;
            set
            {
                maxPreviewCount = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_previewBackgroundColor")] [SerializeField]
        Color previewBackgroundColor = new(0.1764f, 0.1764f, 0.1764f);

        public Color PreviewBackgroundColor
        {
            get => previewBackgroundColor;
            set
            {
                previewBackgroundColor = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showAssetLocationBelowMesh")] [SerializeField]
        bool showAssetLocationBelowMesh = true;

        public bool ShowAssetLocationBelowMesh
        {
            get => showAssetLocationBelowMesh;
            set
            {
                showAssetLocationBelowMesh = value;
                Save(true);
            }
        }

        public bool runtimeMemoryUsageUnderPreview = true;
        public bool showRunTimeMemoryUsageLabel = true;

        #endregion Mesh Preview

        [FormerlySerializedAs("_autoHideSettings")] [SerializeField]
        bool autoHideSettings = true;

        public bool AutoHideSettings
        {
            get => autoHideSettings;
            set
            {
                autoHideSettings = value;
                Save(true);
            }
        }

        #region Information List

        [FormerlySerializedAs("_showInformationFoldout")] [SerializeField]
        bool showInformationFoldout;

        public bool ShowInformationFoldout
        {
            get => showInformationFoldout;
            set
            {
                showInformationFoldout = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showVertexInformation")] [SerializeField]
        bool showVertexInformation = true;

        public bool ShowVertexInformation
        {
            get => showVertexInformation;
            set
            {
                showVertexInformation = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showInformationOnPreview")] [SerializeField]
        bool showInformationOnPreview = true;

        public bool ShowMeshDetailsUnderPreview
        {
            get => showInformationOnPreview;
            set
            {
                showInformationOnPreview = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showTriangleInformation")] [SerializeField]
        bool showTriangleInformation = true;

        public bool ShowTriangleInformation
        {
            get => showTriangleInformation;
            set
            {
                showTriangleInformation = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showEdgeInformation")] [SerializeField]
        bool showEdgeInformation;

        public bool ShowEdgeInformation
        {
            get => showEdgeInformation;
            set
            {
                showEdgeInformation = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showFaceInformation")] [SerializeField]
        bool showFaceInformation;

        public bool ShowFaceInformation
        {
            get => showFaceInformation;
            set
            {
                showFaceInformation = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showTangentInformation")] [SerializeField]
        bool showTangentInformation = true;

        public bool ShowTangentInformation
        {
            get => showTangentInformation;
            set
            {
                showTangentInformation = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showSizeFoldout")] [SerializeField]
        bool showSizeFoldout = false;

        public bool ShowSizeFoldout
        {
            get => showSizeFoldout;
            set
            {
                showSizeFoldout = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showAssetLocationInFoldout")] [SerializeField]
        bool showAssetLocationInFoldout = true;

        public bool ShowAssetLocationInFoldout
        {
            get => showAssetLocationInFoldout;
            set
            {
                showAssetLocationInFoldout = value;
                Save(true);
            }
        }

        #endregion Information List

        #region Gizmo

        [FormerlySerializedAs("_showDebugGizmoFoldout")] [SerializeField]
        bool showDebugGizmoFoldout = true;

        public bool ShowDebugGizmoFoldout
        {
            get => showDebugGizmoFoldout;
            set
            {
                showDebugGizmoFoldout = value;
                Save(true);
            }
        }

        public bool useAntiAliasedGizmo = false;
        public int maximumGizmoDrawTime = 50;

        #endregion Gizmo

        #region Quick actions

        [FormerlySerializedAs("_doNotApplyActionToAsset")] [SerializeField]
        bool doNotApplyActionToAsset = true;

        public bool DoNotApplyActionToAsset
        {
            get => doNotApplyActionToAsset;
            set
            {
                doNotApplyActionToAsset = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showActionsFoldout")] [SerializeField]
        bool showActionsFoldout = true;

        public bool ShowActionsFoldout
        {
            get => showActionsFoldout;
            set
            {
                showActionsFoldout = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showOptimizeButton")] [SerializeField]
        bool showOptimizeButton = true;

        public bool ShowOptimizeButton
        {
            get => showOptimizeButton;
            set
            {
                showOptimizeButton = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showRecalculateNormalsButton")] [SerializeField]
        bool showRecalculateNormalsButton = true;

        public bool ShowRecalculateNormalsButton
        {
            get => showRecalculateNormalsButton;
            set
            {
                showRecalculateNormalsButton = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showRecalculateTangentsButton")] [SerializeField]
        bool showRecalculateTangentsButton = false;

        public bool ShowRecalculateTangentsButton
        {
            get => showRecalculateTangentsButton;
            set
            {
                showRecalculateTangentsButton = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showFlipNormalsButton")] [SerializeField]
        bool showFlipNormalsButton = true;

        public bool ShowFlipNormalsButton
        {
            get => showFlipNormalsButton;
            set
            {
                showFlipNormalsButton = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showGenerateSecondaryUVButton")] [SerializeField]
        bool showGenerateSecondaryUVButton = false;

        public bool ShowGenerateSecondaryUVButton
        {
            get => showGenerateSecondaryUVButton;
            set
            {
                showGenerateSecondaryUVButton = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_showSaveMeshAsButton")] [SerializeField]
        bool showSaveMeshAsButton = true;

        public bool ShowSaveMeshAsButton
        {
            get => showSaveMeshAsButton;
            set
            {
                showSaveMeshAsButton = value;
                Save(true);
            }
        }

        #endregion Quick actions

        public List<OriginalMaterial> originalMaterials = new();

        public bool showDefaultSkinnedMeshRendererInspector = true;
        public int updateCacheEveryDashSeconds = 1;

        public bool animatedFoldout = true;

        #region Reset

        public void ResetToDefault()
        {
            showInformationFoldout = false;

            showInformationOnPreview = true;
            runtimeMemoryUsageUnderPreview = true;
            showRunTimeMemoryUsageLabel = true;

            showSizeFoldout = true;
            showAssetLocationBelowMesh = false;
            showActionsFoldout = true;
            showDebugGizmoFoldout = true;

            Reset();
        }

        public void ResetToDefault2()
        {
            showInformationFoldout = true;
            showAssetLocationInFoldout = true;

            showInformationOnPreview = false;
            runtimeMemoryUsageUnderPreview = false;
            showRunTimeMemoryUsageLabel = true;

            showSizeFoldout = true;
            showAssetLocationBelowMesh = false;
            showActionsFoldout = true;
            showDebugGizmoFoldout = true;

            Reset();
        }

        public void ResetToMinimal()
        {
            showInformationFoldout = false;
            showInformationOnPreview = true;
            showSizeFoldout = false;
            showAssetLocationBelowMesh = false;
            showActionsFoldout = false;
            showDebugGizmoFoldout = false;
            showRunTimeMemoryUsageLabel = false;

            Reset();
        }

        public void ResetToNothing()
        {
            showInformationFoldout = false;
            showInformationOnPreview = false;
            showSizeFoldout = false;
            showAssetLocationBelowMesh = false;
            showActionsFoldout = false;
            showDebugGizmoFoldout = false;

            Reset();
        }

        void Reset()
        {
            selectedUnit = 0;

            autoHideSettings = true;

            showMeshPreview = true;
            maxPreviewCount = 10;
            meshPreviewHeight = 200;

            showVertexInformation = true;
            showTriangleInformation = true;
            showEdgeInformation = false;
            showFaceInformation = false;
            showTangentInformation = true;

            showOptimizeButton = true;
            showRecalculateNormalsButton = true;
            showRecalculateTangentsButton = false;
            showFlipNormalsButton = true;
            showGenerateSecondaryUVButton = true;
            showSaveMeshAsButton = true;

            doNotApplyActionToAsset = true;

            previewBackgroundColor = new(0.1764f, 0.1764f, 0.1764f);
            overrideInspectorColor = false;
            inspectorColor = new(0, 0, 1, 0.025f);
            overrideFoldoutColor = false;
            foldoutColor = new(0, 1, 0, 0.025f);

            animatedFoldout = true;

            Save();
        }

        #endregion Reset

        public void Save()
        {
            Save(true);
        }
    }

    [System.Serializable]
    public class OriginalMaterial
    {
        public Material[] materials;

        public OriginalMaterial(Material[] materials)
        {
            this.materials = materials;
        }
    }
}