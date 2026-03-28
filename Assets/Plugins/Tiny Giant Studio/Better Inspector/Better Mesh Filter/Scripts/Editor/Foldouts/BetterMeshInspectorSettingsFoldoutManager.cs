using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    public class
        BetterMeshInspectorSettingsFoldoutManager //That's one giant name, but I want names to be clear and not get mixed with other settings scripts
    {
        readonly BetterMeshSettings _editorSettings;

        readonly VisualElement _root;
        readonly GroupBox _inspectorSettingsFoldout;
        readonly Toggle _inspectorSettingsFoldoutToggle;

        bool _inspectorFoldoutSetupCompleted = false;

        Toggle _overrideInspectorColorToggle;
        ColorField _inspectorColorField;
        Toggle _overrideFoldoutColorToggle;
        ColorField _foldoutColorField;
        SliderInt _foldoutStyle;
        SliderInt _buttonStyle;

        const string AssetLink =
            "https://assetstore.unity.com/packages/tools/utilities/better-mesh-filter-266489?aid=1011ljxWe";

        const string PublisherLink = "https://assetstore.unity.com/publishers/45848?aid=1011ljxWe";
        const string DocumentationLink = "https://ferdowsur.gitbook.io/better-mesh/";

        Toggle _autoHideInspectorSettingsField;
        Toggle _showMeshFieldOnTop;
        Toggle _showAssetLocationBelowPreviewField;
        Toggle _showAssetLocationInFoldoutToggle;

        Toggle _showMeshPreviewField;
        SliderInt _maxPreviewAmount;

        Toggle _showInformationOnPreviewField;
        Toggle _showRuntimeMemoryUsageUnderPreview;
        Toggle _runTimeMemoryUsageLabelToggle;

        Toggle _showMeshSizeField;

        Toggle _showMeshDetailsInFoldoutToggle;
        Toggle _showVertexInformationToggle;
        Toggle _showTriangleInformationToggle;
        Toggle _showEdgeInformationToggle;
        Toggle _showFaceInformationToggle;
        Toggle _showTangentInformationToggle;

        Toggle _showDebugGizmoFoldoutField;


        public event Action OnPreviewSettingsUpdated;
        public event Action OnMeshFieldPositionUpdated;
        public event Action OnMeshLocationSettingsUpdated;
        public event Action OnActionButtonsSettingsUpdated;
        public event Action OnBaseSizeSettingsUpdated;
        public event Action OnDebugGizmoSettingsUpdated;

        public BetterMeshInspectorSettingsFoldoutManager(BetterMeshSettings editorSettings, VisualElement root)
        {
            _inspectorFoldoutSetupCompleted = false; //Unnecessary. This is the default value

            _root = root;
            _editorSettings = editorSettings;

            _inspectorSettingsFoldout = root.Q<GroupBox>("InspectorSettings");
            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout);

            _inspectorSettingsFoldoutToggle = _inspectorSettingsFoldout.Q<Toggle>("FoldoutToggle");

            _inspectorSettingsFoldout.style.display = DisplayStyle.None;
            UpdateInspectorColor();
        }

        public void ToggleInspectorSettings()
        {
            if (IsInspectorSettingsIsHidden())
            {
                //This completes the setup of the inspector foldout.
                //This is made to avoid referencing everything at the start,
                //Giving this a performance improvement
                if (!_inspectorFoldoutSetupCompleted) Setup();

                UpdateInspectorSettingsFoldout();

                _inspectorSettingsFoldout.style.display = DisplayStyle.Flex;
                _inspectorSettingsFoldoutToggle.value = true;
            }
            else
            {
                HideSettings();
            }
        }

        /// <summary>
        /// Returns true when the foldout is hidden.
        /// </summary>
        /// <returns></returns>
        public bool IsInspectorSettingsIsHidden() => _inspectorSettingsFoldout.style.display == DisplayStyle.None;


        void Setup()
        {
            SettingsFilePathManager.SetupSettingsPathConfigurationUI(_inspectorSettingsFoldout);
            
            _inspectorSettingsFoldoutToggle.RegisterValueChangedCallback(ev =>
            {
                if (_editorSettings.AutoHideSettings)
                {
                    _inspectorSettingsFoldout.style.display = ev.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                }
            });

            #region Inspector Customization

            Toggle foldoutAnimationsToggle = _inspectorSettingsFoldout.Q<Toggle>("FoldoutAnimationsToggle");
            foldoutAnimationsToggle.SetValueWithoutNotify(_editorSettings.animatedFoldout);
            foldoutAnimationsToggle.RegisterValueChangedCallback(e =>
            {
                _editorSettings.animatedFoldout = e.newValue;
                _editorSettings.Save();

                StyleSheetsManager.UpdateStyleSheet(_root);
            });

            _foldoutColorField = _inspectorSettingsFoldout.Q<ColorField>("FoldoutColorField");
            _inspectorColorField = _inspectorSettingsFoldout.Q<ColorField>("InspectorColorField");

            _overrideInspectorColorToggle = _inspectorSettingsFoldout.Q<Toggle>("OverrideInspectorColorToggle");
            _overrideInspectorColorToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.OverrideInspectorColor = ev.newValue;
                UpdateInspectorColor();
            });
            _inspectorColorField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.InspectorColor = ev.newValue;
                UpdateInspectorColor();
            });
            _overrideFoldoutColorToggle = _inspectorSettingsFoldout.Q<Toggle>("OverrideFoldoutColorToggle");
            _overrideFoldoutColorToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.OverrideFoldoutColor = ev.newValue;
                UpdateInspectorColor();
            });
            _foldoutColorField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.FoldoutColor = ev.newValue;
                UpdateInspectorColor();
            });
            _foldoutStyle = _inspectorSettingsFoldout.Q<SliderInt>("FoldoutStyle");
            _foldoutStyle.schedule.Execute(() =>
            {
                _foldoutStyle.RegisterValueChangedCallback(ev =>
                {
                    BetterInspectorEditorSettings.instance.selectedFoldoutStyle = ev.newValue;
                    StyleSheetsManager.UpdateStyleSheet(_root); 
                });
            }).ExecuteLater(75);   
            _buttonStyle = _inspectorSettingsFoldout.Q<SliderInt>("ButtonStyle");
            _buttonStyle.schedule.Execute(() =>
            {
                _buttonStyle.RegisterValueChangedCallback(ev =>
                {
                    BetterInspectorEditorSettings.instance.selectedButtonStyle = ev.newValue;
                    StyleSheetsManager.UpdateStyleSheet(_root); 
                });
            }).ExecuteLater(1000);
            #endregion Inspector Customization

            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout.Q<GroupBox>("InspectorCustomizationFoldout"));
            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout.Q<GroupBox>("MeshPreviewSettingsFoldout"),
                "FoldoutToggle", "showMeshPreview");
            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout.Q<GroupBox>("InformationFoldoutSettingsFoldout"),
                "FoldoutToggle", "ShowMeshDetailsInFoldoutToggle");
            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout.Q<GroupBox>("MeshDetailsSettingsFoldout"));
            CustomFoldout.SetupFoldout(_inspectorSettingsFoldout.Q<GroupBox>("ActionSettingsFoldout"),
                "FoldoutToggle", "showActionsFoldout");

            _inspectorSettingsFoldout.Q<ToolbarButton>("AssetLink").clicked += () =>
            {
                Application.OpenURL(AssetLink);
            };
            _inspectorSettingsFoldout.Q<ToolbarButton>("Documentation").clicked += () =>
            {
                Application.OpenURL(DocumentationLink);
            };
            _inspectorSettingsFoldout.Q<ToolbarButton>("OtherAssetsLink").clicked += () =>
            {
                Application.OpenURL(PublisherLink);
            };

            _inspectorSettingsFoldout.Q<Button>("ResetInspectorSettings").clicked += ResetInspectorSettings;
            _inspectorSettingsFoldout.Q<Button>("ResetInspectorSettings2").clicked += ResetInspectorSettings2;
            _inspectorSettingsFoldout.Q<Button>("ResetInspectorSettingsToMinimal").clicked +=
                ResetInspectorSettingsToMinimal;
            _inspectorSettingsFoldout.Q<Button>("ResetInspectorSettingsToNothing").clicked +=
                ResetInspectorSettingsToNothing;

            _autoHideInspectorSettingsField = _inspectorSettingsFoldout.Q<Toggle>("autoHideInspectorSettings");
            _autoHideInspectorSettingsField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.AutoHideSettings = ev.newValue;
            });

            _showMeshFieldOnTop = _inspectorSettingsFoldout.Q<Toggle>("MeshFieldOnTop");
            _showMeshFieldOnTop.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.meshFieldOnTop = ev.newValue;
                OnMeshFieldPositionUpdated?.Invoke();
            });

            _showMeshPreviewField = _inspectorSettingsFoldout.Q<Toggle>("showMeshPreview");
            _showMeshPreviewField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowMeshPreview = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _maxPreviewAmount = _inspectorSettingsFoldout.Q<SliderInt>("MaximumPreviewAmount");
            _maxPreviewAmount.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.MaxPreviewCount = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });


            _showInformationOnPreviewField = _inspectorSettingsFoldout.Q<Toggle>("ShowInformationOnPreview");
            _showInformationOnPreviewField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowMeshDetailsUnderPreview = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _showRuntimeMemoryUsageUnderPreview =
                _inspectorSettingsFoldout.Q<Toggle>("ShowRuntimeMemoryUsageBelowPreview");
            _runTimeMemoryUsageLabelToggle = _inspectorSettingsFoldout.Q<Toggle>("RunTimeMemoryUsageLabelToggle");

            _showRuntimeMemoryUsageUnderPreview.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.runtimeMemoryUsageUnderPreview = ev.newValue;
                _editorSettings.Save();

                OnPreviewSettingsUpdated?.Invoke();
            });

            _runTimeMemoryUsageLabelToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.showRunTimeMemoryUsageLabel = ev.newValue;
                _editorSettings.Save();

                OnPreviewSettingsUpdated?.Invoke();
            });

            _showAssetLocationBelowPreviewField = _inspectorSettingsFoldout.Q<Toggle>("ShowAssetLocationBelowPreview");
            _showAssetLocationBelowPreviewField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowAssetLocationBelowMesh = ev.newValue;
                OnMeshLocationSettingsUpdated?.Invoke();
            });

            _showAssetLocationInFoldoutToggle = _inspectorSettingsFoldout.Q<Toggle>("ShowAssetLocationInFoldout");
            _showAssetLocationInFoldoutToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowAssetLocationInFoldout = ev.newValue;

                OnPreviewSettingsUpdated?.Invoke();
            });

            _showMeshSizeField = _inspectorSettingsFoldout.Q<Toggle>("ShowMeshSize");
            _showMeshSizeField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowSizeFoldout = ev.newValue;

                OnBaseSizeSettingsUpdated?.Invoke();
            });


            #region Mesh Details

            _showMeshDetailsInFoldoutToggle = _inspectorSettingsFoldout.Q<Toggle>("ShowMeshDetailsInFoldoutToggle");
            _showMeshDetailsInFoldoutToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowInformationFoldout = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _showVertexInformationToggle = _inspectorSettingsFoldout.Q<Toggle>("showVertexCount");
            _showVertexInformationToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowVertexInformation = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _showTriangleInformationToggle = _inspectorSettingsFoldout.Q<Toggle>("showTriangleCount");
            _showTriangleInformationToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowTriangleInformation = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _showEdgeInformationToggle = _inspectorSettingsFoldout.Q<Toggle>("showEdgeCount");
            _showEdgeInformationToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowEdgeInformation = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            _showFaceInformationToggle = _inspectorSettingsFoldout.Q<Toggle>("showFaceCount");
            _showFaceInformationToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowFaceInformation = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });
            _showTangentInformationToggle = _inspectorSettingsFoldout.Q<Toggle>("showTangentCount");
            _showTangentInformationToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowTangentInformation = ev.newValue;
                OnPreviewSettingsUpdated?.Invoke();
            });

            #endregion

            #region Actions

            Toggle showActionsFoldoutField = _inspectorSettingsFoldout.Q<Toggle>("showActionsFoldout");

            showActionsFoldoutField.SetValueWithoutNotify(_editorSettings.ShowActionsFoldout);
            showActionsFoldoutField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowActionsFoldout = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            Toggle showOptimizeButtonToggle = _inspectorSettingsFoldout.Q<Toggle>("OptimizeMesh");
            showOptimizeButtonToggle.SetValueWithoutNotify(_editorSettings.ShowOptimizeButton);
            showOptimizeButtonToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowOptimizeButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });
            Toggle recalculateNormalsToggle = _inspectorSettingsFoldout.Q<Toggle>("RecalculateNormals");
            recalculateNormalsToggle.SetValueWithoutNotify(_editorSettings.ShowRecalculateNormalsButton);
            recalculateNormalsToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowRecalculateNormalsButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            Toggle showRecalculateTangentsButtonToggle = _inspectorSettingsFoldout.Q<Toggle>("RecalculateTangents");
            showRecalculateTangentsButtonToggle.SetValueWithoutNotify(_editorSettings.ShowRecalculateTangentsButton);
            showRecalculateTangentsButtonToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowRecalculateTangentsButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            Toggle showFlipNormalsToggle = _inspectorSettingsFoldout.Q<Toggle>("FlipNormals");
            showFlipNormalsToggle.SetValueWithoutNotify(_editorSettings.ShowFlipNormalsButton);
            showFlipNormalsToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowFlipNormalsButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            Toggle showGenerateSecondaryUVButtonToggle = _inspectorSettingsFoldout.Q<Toggle>("GenerateSecondaryUVSet");
            showGenerateSecondaryUVButtonToggle.SetValueWithoutNotify(_editorSettings.ShowGenerateSecondaryUVButton);
            showGenerateSecondaryUVButtonToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowGenerateSecondaryUVButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            Toggle showSaveMeshButtonAsToggle = _inspectorSettingsFoldout.Q<Toggle>("SaveMeshAs");
            showSaveMeshButtonAsToggle.SetValueWithoutNotify(_editorSettings.ShowSaveMeshAsButton);
            showSaveMeshButtonAsToggle.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowSaveMeshAsButton = ev.newValue;
                OnActionButtonsSettingsUpdated?.Invoke();
            });

            #endregion

            _showDebugGizmoFoldoutField = _inspectorSettingsFoldout.Q<Toggle>("showDebugGizmoFoldout");
            _showDebugGizmoFoldoutField.RegisterValueChangedCallback(ev =>
            {
                _editorSettings.ShowDebugGizmoFoldout = ev.newValue;
                OnDebugGizmoSettingsUpdated?.Invoke();
            });

            Button scaleSettingsButton = _inspectorSettingsFoldout.Q<Button>("ScaleSettingsButton");
            scaleSettingsButton.clicked += () =>
            {
                SettingsService.OpenProjectSettings("Project/Tiny Giant Studio/Scale Settings");
                GUIUtility.ExitGUI();
            };

            _inspectorFoldoutSetupCompleted = true;
        }

        /// <summary>
        /// This updates the fields that show the current inspector settings
        /// </summary>
        void UpdateInspectorSettingsFoldout()
        {
            _overrideInspectorColorToggle.SetValueWithoutNotify(_editorSettings.OverrideInspectorColor);
            _inspectorColorField.SetValueWithoutNotify(_editorSettings.InspectorColor);
            _overrideFoldoutColorToggle.SetValueWithoutNotify(_editorSettings.OverrideFoldoutColor);
            _foldoutColorField.SetValueWithoutNotify(_editorSettings.FoldoutColor);
            _foldoutStyle.SetValueWithoutNotify(BetterInspectorEditorSettings.instance.selectedFoldoutStyle);
            _buttonStyle.SetValueWithoutNotify(BetterInspectorEditorSettings.instance.selectedButtonStyle);

            _autoHideInspectorSettingsField.SetValueWithoutNotify(_editorSettings.AutoHideSettings);
            _showMeshFieldOnTop.SetValueWithoutNotify(_editorSettings.meshFieldOnTop);

            _showMeshPreviewField.SetValueWithoutNotify(_editorSettings.ShowMeshPreview);
            _maxPreviewAmount.SetValueWithoutNotify(_editorSettings.MaxPreviewCount);

            _showInformationOnPreviewField.SetValueWithoutNotify(_editorSettings.ShowMeshDetailsUnderPreview);

            _showRuntimeMemoryUsageUnderPreview.SetValueWithoutNotify(_editorSettings.runtimeMemoryUsageUnderPreview);
            _runTimeMemoryUsageLabelToggle.SetValueWithoutNotify(_editorSettings.showRunTimeMemoryUsageLabel);

            _showMeshSizeField.SetValueWithoutNotify(_editorSettings.ShowSizeFoldout);
            _showAssetLocationBelowPreviewField.SetValueWithoutNotify(_editorSettings.ShowAssetLocationBelowMesh);
            _showAssetLocationInFoldoutToggle.SetValueWithoutNotify(_editorSettings.ShowAssetLocationInFoldout);

            _showMeshDetailsInFoldoutToggle.SetValueWithoutNotify(_editorSettings.ShowInformationFoldout);
            _showVertexInformationToggle.SetValueWithoutNotify(_editorSettings.ShowVertexInformation);
            _showTriangleInformationToggle.SetValueWithoutNotify(_editorSettings.ShowTriangleInformation);
            _showEdgeInformationToggle.SetValueWithoutNotify(_editorSettings.ShowEdgeInformation);
            _showFaceInformationToggle.SetValueWithoutNotify(_editorSettings.ShowFaceInformation);
            _showTangentInformationToggle.SetValueWithoutNotify(_editorSettings.ShowTangentInformation);

            _showDebugGizmoFoldoutField.SetValueWithoutNotify(_editorSettings.ShowDebugGizmoFoldout);
        }

        public void HideSettings()
        {
            _inspectorSettingsFoldout.style.display = DisplayStyle.None;
            _inspectorSettingsFoldoutToggle.value = false;
        }


        void ResetInspectorSettings()
        {
            _editorSettings.ResetToDefault();
            EditorSettingsHaveBeenReset();
        }

        void ResetInspectorSettings2()
        {
            _editorSettings.ResetToDefault2();
            EditorSettingsHaveBeenReset();
        }

        void ResetInspectorSettingsToMinimal()
        {
            _editorSettings.ResetToMinimal();
            EditorSettingsHaveBeenReset();
        }

        void ResetInspectorSettingsToNothing()
        {
            _editorSettings.ResetToNothing();
            EditorSettingsHaveBeenReset();
        }

        void EditorSettingsHaveBeenReset()
        {
            BetterInspectorEditorSettings.instance.Reset();
            StyleSheetsManager.UpdateStyleSheet(_root);
            UpdateInspectorSettingsFoldout();
            UpdateInspectorColor();

            OnPreviewSettingsUpdated?.Invoke();
            OnMeshFieldPositionUpdated?.Invoke();
            OnMeshLocationSettingsUpdated?.Invoke();
            OnActionButtonsSettingsUpdated?.Invoke();
            OnBaseSizeSettingsUpdated?.Invoke();
            OnDebugGizmoSettingsUpdated?.Invoke();
        }

        void UpdateInspectorColor()
        {
            List<GroupBox> customFoldoutGroups = _root.Query<GroupBox>(className: "custom-foldout").ToList();
            if (_editorSettings.OverrideFoldoutColor)
            {
                _foldoutColorField?.SetEnabled(true);

                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = _editorSettings.FoldoutColor;
            }
            else
            {
                _foldoutColorField?.SetEnabled(false);
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = StyleKeyword.Null;
            }

            if (_editorSettings.OverrideInspectorColor)
            {
                _inspectorColorField?.SetEnabled(true);
                _root.Q<GroupBox>("RootHolder").style.backgroundColor = _editorSettings.InspectorColor;
            }
            else
            {
                _inspectorColorField?.SetEnabled(false);
                _root.Q<GroupBox>("RootHolder").style.backgroundColor = StyleKeyword.Null;
            }
        }
    }
}