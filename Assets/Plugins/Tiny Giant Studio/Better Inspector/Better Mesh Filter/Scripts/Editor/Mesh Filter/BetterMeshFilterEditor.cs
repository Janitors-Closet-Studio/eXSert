using System.Collections.Generic;
using System.Linq;
using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    /// <summary>
    /// Methods containing the word Setup are called once when the inspector is created.
    /// Methods containing the word Update can be called multiple times to update to reflect changes.
    ///
    ///
    /// To-do:
    /// Get editor settings from update
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MeshFilter))]
    public class BetterMeshFilterEditor : Editor
    {
        #region Variable Declarations

        /// <summary>
        /// If reference is lost, retrieved from file location
        /// </summary>
        [SerializeField] VisualTreeAsset visualTreeAsset;

        const string VisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Mesh Filter/Scripts/Editor/Mesh Filter/BetterMeshFilter.uxml";

        const string VisualTreeAssetGuid = "bb16fd1ddd2f41648b64223b6c226839";

        VisualElement _root;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        List<Mesh> _meshes = new();
        readonly List<Transform> _transforms = new();

        Button _settingsButton;
        GenericDropdownMenu _settingsButtonContextMenu;

        ObjectField _meshField;

        Editor _originalEditor;

        BetterMeshPreviewManager _previewManager;
        BaseSizeFoldoutManager _baseSizeFoldoutManager;
        ActionsFoldoutManager _actionsFoldoutManager;
        DebugGizmoManager _debugGizmoManager;
        BetterMeshInspectorSettingsFoldoutManager _settingsFoldoutManager;

        BetterMeshSettings _editorSettings;

        Label _assetLocationOutsideFoldout;
        Label _assetLocationLabel;

        #endregion Variable Declarations

        #region Unity Stuff

        //This is not unnecessary.
        void OnDestroy()
        {
            CleanUp();
        }

        void OnDisable()
        {
            CleanUp();
        }

        /// <summary>
        /// CreateInspectorGUI is called each time something else is selected with this one locked.
        /// </summary>
        /// <returns></returns>
        public override VisualElement CreateInspectorGUI()
        {
            _root = new();

            if (target == null)
                return _root;

            //In-case reference to the asset is lost, retrieve it from the file location
            if (visualTreeAsset == null) visualTreeAsset = Utility.GetVisualTreeAsset(VisualTreeAssetFileLocation, VisualTreeAssetGuid);

            //If it can't find the BetterMeshUXML,
            //Show the default inspector
            if (visualTreeAsset == null)
            {
                LoadDefaultEditor();
                return _root;
            }

            visualTreeAsset.CloneTree(_root);

            _editorSettings = BetterMeshSettings.instance;

            StyleSheetsManager.UpdateStyleSheet(_root);

            _debugGizmoManager =
                new(_editorSettings,
                    _root); //This needs to be set up before mesh field because mesh field will pass the meshes list to debugList
            _baseSizeFoldoutManager =
                new(_editorSettings,
                    _root); //This needs to be set up before mesh field because mesh field will pass the meshes list to debugList

            SetupMeshField();

            _actionsFoldoutManager = new(_editorSettings, _root, targets);
            _settingsFoldoutManager = new(_editorSettings, _root);
            _settingsFoldoutManager.OnMeshFieldPositionUpdated += UpdateMeshFieldGroupPosition;
            _settingsFoldoutManager.OnMeshLocationSettingsUpdated += UpdateMeshTexts;
            _settingsFoldoutManager.OnDebugGizmoSettingsUpdated += _debugGizmoManager.UpdateDisplayStyle;
            _settingsFoldoutManager.OnActionButtonsSettingsUpdated += _actionsFoldoutManager.UpdateFoldoutVisibilities;
            _settingsFoldoutManager.OnBaseSizeSettingsUpdated += BaseSizeSettingUpdated;


            _previewManager = new(_editorSettings, _root);
            _previewManager.SetupPreviewManager(_meshes, targets.Length);
            _settingsFoldoutManager.OnPreviewSettingsUpdated += UpdatePreviews;

            _settingsButton = _root.Q<Button>("SettingsButton");
            _settingsButton.clicked += OpenContextMenu_settingsButton;

            return _root;
        }

        void BaseSizeSettingUpdated()
        {
            _baseSizeFoldoutManager?.UpdateTargets(_meshes);
        }

        void UpdateMeshFieldGroupPosition()
        {
            GroupBox meshFieldGroupBox = _root.Q<GroupBox>("MeshFieldGroupBox");
            if (_editorSettings.meshFieldOnTop)
                _root.Q<GroupBox>("RootHolder").Insert(2, meshFieldGroupBox);
            else
                _root.Q<VisualElement>("MainContainer").Insert(0, meshFieldGroupBox);
        }

        #endregion Unity Stuff

        void SetupMeshField()
        {
            _meshField = _root.Q<ObjectField>("mesh");
            _assetLocationOutsideFoldout = _root.Q<Label>("AssetLocationOutsideFoldout");
            _assetLocationLabel = _root.Q<Label>("assetLocation");

            UpdateMeshesReferences();

            _meshField.schedule.Execute(RegisterMeshField).ExecuteLater(10); //1000 ms = 1 s

            UpdateMeshTexts();
            UpdateMeshFieldGroupPosition();
        }

        void RegisterMeshField()
        {
            _meshField.RegisterValueChangedCallback(_ =>
            {
                UpdateMeshesReferences();
                UpdateMeshTexts();

                _actionsFoldoutManager?.MeshUpdated();

                _previewManager?.CreatePreviews(_meshes);

                if (_meshes.Count == 0)
                    HideAllFoldouts();
            });
        }

        /// <summary>
        /// This updates the tool-tip and labels with asset location
        /// </summary>
        void UpdateMeshTexts()
        {
            string assetPath;
            if (_meshes == null) assetPath = "";
            else
                assetPath = _meshes.Count switch
                {
                    0 => "No mesh found.",
                    > 1 => "",
                    1 when _meshes[0] != null && AssetDatabase.Contains(_meshes[0]) => AssetDatabase.GetAssetPath(
                        _meshes[0]),
                    _ => "The mesh is not connected to an asset."
                };

            _meshField.tooltip = assetPath;
            _assetLocationOutsideFoldout.text = assetPath;
            _assetLocationLabel.text = assetPath;

            if (targets.Length != 1)
            {
                _assetLocationOutsideFoldout.style.display = DisplayStyle.None;
                _assetLocationLabel.style.display = DisplayStyle.None;

                return;
            }

            _assetLocationOutsideFoldout.style.display =
                _editorSettings.ShowAssetLocationBelowMesh ? DisplayStyle.Flex : DisplayStyle.None;

            _assetLocationLabel.style.display =
                _editorSettings.ShowAssetLocationInFoldout ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void UpdateMeshesReferences()
        {
            _meshes.Clear();
            _transforms.Clear();

            foreach (MeshFilter meshFilter in targets.Cast<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null) continue;
                _meshes.Add(meshFilter.sharedMesh);
                _transforms.Add(meshFilter.transform);
            }

            _debugGizmoManager?.UpdateTargets(_meshes, _transforms);
            _baseSizeFoldoutManager?.UpdateTargets(_meshes);
        }

        #region Foldouts

        void HideAllFoldouts()
        {
            _previewManager.HideInformationFoldout();
            _debugGizmoManager.HideDebugGizmo();
            _settingsFoldoutManager.HideSettings();
            _baseSizeFoldoutManager.HideFoldout();
        }

        #endregion Foldouts

        #region Settings

        #region Setup

        void UpdatePreviews()
        {
            _previewManager.CreatePreviews(_meshes);
        }

        #endregion Setup

        #endregion Settings

        #region Functions

        /// <summary>
        /// This cleans up memory for the previews, textures and editors that can be created by the asset
        /// </summary>
        void CleanUp()
        {
            _previewManager?.CleanUp();

            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);

            _debugGizmoManager?.Cleanup();
        }

        /// <summary>
        /// If the UXML file is missing for any reason,
        /// Instead of showing an empty inspector,
        /// This loads the default one.
        /// This shouldn't ever happen.
        /// </summary>
        void LoadDefaultEditor()
        {
            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);

            _originalEditor = CreateEditor(targets);
            IMGUIContainer inspectorContainer = new(OnGUICallback);
            _root.Add(inspectorContainer);
        }

        //For the original Editor
        void OnGUICallback()
        {
            //EditorGUIUtility.hierarchyMode = true;

            EditorGUI.BeginChangeCheck();
            _originalEditor.OnInspectorGUI();
            EditorGUI.EndChangeCheck();
        }

        void OpenContextMenu_settingsButton()
        {
            UpdateContextMenu_settingsButton();
            _settingsButtonContextMenu.DropDown(GetMenuRect(_settingsButton), _settingsButton, true);
        }

        void UpdateContextMenu_settingsButton()
        {
            _settingsButtonContextMenu = new();

            bool isChecked = !_settingsFoldoutManager.IsInspectorSettingsIsHidden();

            _settingsButtonContextMenu.AddItem("Settings", isChecked,
                () => { _settingsFoldoutManager.ToggleInspectorSettings(); });
        }

        static Rect GetMenuRect(VisualElement anchor)
        {
            Rect worldBound = anchor.worldBound;
            worldBound.xMin -= 150;
            worldBound.xMax += 0;
            return worldBound;
        }

        #endregion Functions

        void OnSceneGUI()
        {
            _debugGizmoManager?.DrawGizmo();
        }
    }
}