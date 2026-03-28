using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    /// <summary>
    ///
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SkinnedMeshRenderer))]
    public class SkinnedMeshRendererEditor : Editor
    {
        #region Variables

        SkinnedMeshRenderer[] _skinnedMeshRenderers;

        List<Mesh> _meshes = new();
        List<Transform> _transforms = new();


        [SerializeField] VisualTreeAsset visualTreeAsset;

        const string VisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Mesh Filter/Scripts/Editor/Skinned Mesh Renderer/BetterSkinnedMeshRenderer.uxml";

        const string VisualTreeAssetGuid = "4855344e42fb2bd40aa29c60be87912d";
        
        Editor _originalEditor;
        VisualElement _root;

        BetterMeshSettings _editorSettings;
        BetterMeshInspectorSettingsFoldoutManager _settingsFoldoutManager;

        BetterMeshPreviewManager _previewManager;
        BaseSizeFoldoutManager _baseSizeFoldoutManager;
        ActionsFoldoutManager _actionsFoldoutManager;
        DebugGizmoManager _debugGizmoManager;


        const string BoundingVolumeEditButtonTooltip =
            "Edit bounding volume in the scene view.\n\n  - Hold Alt after clicking control handle to pin center in place.\n  - Hold Shift after clicking control handle to scale uniformly.";

        #region UI

        ObjectField _meshField;
        HelpBox _invalidMeshWarning;
        Label _assetLocationOutsideFoldout;
        Label _assetLocationLabel;

        GroupBox _blendShapesFoldout;

        GroupBox _boundsGroupBox;
        PropertyField _aabb;

        Button _settingsButton;

        #endregion UI

        GenericDropdownMenu _settingsButtonContextMenu;

        #endregion Variables

        #region Unity stuff

        //This is not unnecessary.
        void OnDestroy()
        {
            CleanUp();
        }

        void OnDisable()
        {
            CleanUp();
        }

        public override VisualElement CreateInspectorGUI()
        {
            _root = new();

            if (target == null)
                return _root;

            _skinnedMeshRenderers = new SkinnedMeshRenderer[targets.Length];
            for (int i = 0; i < _skinnedMeshRenderers.Length; i++)
            {
                _skinnedMeshRenderers[i] = (SkinnedMeshRenderer)targets[i];
            }

            //In-case reference to the asset is lost, retrieve it from the file location
            visualTreeAsset = Utility.GetVisualTreeAsset(VisualTreeAssetFileLocation, VisualTreeAssetGuid);

            if (visualTreeAsset == null) //if it couldn't find the asset, load the default inspector instead of showing an empty section.
            {
                LoadDefaultEditor(_root);
                return _root;
            }

            visualTreeAsset.CloneTree(_root);

            _editorSettings = BetterMeshSettings.instance;
            
            StyleSheetsManager.UpdateStyleSheet(_root);

            _debugGizmoManager = new(_editorSettings, _root);
            _baseSizeFoldoutManager = new(_editorSettings, _root);

            SetupMeshFieldGroup();

            SetupBlendShapes();

            SetupBoundingVolume();

            SetupLightingFoldout();

            SetupProbesFoldout();

            _actionsFoldoutManager = new(_editorSettings, _root, targets);

            SetupDebugFoldout();

            SetupAdditionalSettings();

            UpdateInspectorOverride();

            _settingsFoldoutManager = new(_editorSettings, _root);

            _settingsFoldoutManager.OnMeshFieldPositionUpdated += UpdateMeshFieldGroupPosition;
            _settingsFoldoutManager.OnMeshLocationSettingsUpdated += UpdateMeshTexts;
            _settingsFoldoutManager.OnDebugGizmoSettingsUpdated += _debugGizmoManager.UpdateDisplayStyle;
            _settingsFoldoutManager.OnDebugGizmoSettingsUpdated += UpdateDebugFoldoutVisibility;
            _settingsFoldoutManager.OnActionButtonsSettingsUpdated += _actionsFoldoutManager.UpdateFoldoutVisibilities;
            _settingsFoldoutManager.OnBaseSizeSettingsUpdated += BaseSizeSettingUpdated;

            
            _previewManager = new(_editorSettings, _root);
            _previewManager.SetupPreviewManager(_meshes, targets.Length);
            _settingsFoldoutManager.OnPreviewSettingsUpdated += UpdatePreviews;

            _settingsButton = _root.Q<Button>("SettingsButton");
            _settingsButton.clicked += OpenContextMenu_settingsButton;

            _root.Q<Label>("BonesCounter").text = GetBonesCount().ToString(CultureInfo.InvariantCulture);

            return _root;
        }

        int GetBonesCount() => _skinnedMeshRenderers.Sum(item => item.bones.Length);

        void UpdatePreviews() => _previewManager.CreatePreviews(_meshes);

        void BaseSizeSettingUpdated()
        {
            _baseSizeFoldoutManager?.UpdateTargets(_meshes);
        }

        void UpdateInspectorOverride()
        {
            VisualElement overrideContainer = _root.Q<VisualElement>("OverrideOfTheDefaultInspector");
            VisualElement defaultContainer = _root.Q<VisualElement>("DefaultInspectorContainer");
            if (!_editorSettings.showDefaultSkinnedMeshRendererInspector && _skinnedMeshRenderers.Length == 1)
            {
                overrideContainer.style.display = DisplayStyle.Flex;
                defaultContainer.style.display = DisplayStyle.None;
            }
            else
            {
                overrideContainer.style.display = DisplayStyle.None;
                defaultContainer.style.display = DisplayStyle.Flex;
                LoadDefaultEditor(defaultContainer);
            }
        }

        #endregion Unity stuff

        #region MeshField

        void SetupMeshFieldGroup()
        {
            _meshField = _root.Q<ObjectField>("MeshField");
            _assetLocationOutsideFoldout = _root.Q<Label>("AssetLocationOutsideFoldout");
            _assetLocationLabel = _root.Q<Label>("assetLocation");

            UpdateMeshReferences();

            _meshField.schedule.Execute(RegisterMeshField).ExecuteLater(1); //1000 ms = 1 s

            UpdateMeshSelectionWarnings();
            UpdateMeshTexts();
            UpdateMeshFieldGroupPosition();
        }

        void RegisterMeshField()
        {
            _meshField.RegisterValueChangedCallback(_ =>
            {
                UpdateMeshSelectionWarnings();
                UpdateBlendShapes();

                UpdateMeshReferences();
                UpdateMeshTexts();

                _actionsFoldoutManager?.MeshUpdated();
                _previewManager?.CreatePreviews(_meshes);

                if (_meshes.Count == 0)
                    HideAllFoldouts();
            });
        }

        void HideAllFoldouts()
        {
            _previewManager.HideInformationFoldout();
            _debugGizmoManager.HideDebugGizmo();
            _settingsFoldoutManager.HideSettings();
            _baseSizeFoldoutManager.HideFoldout();
        }

        void UpdateMeshReferences()
        {
            _meshes.Clear();
            _transforms.Clear();

            foreach (SkinnedMeshRenderer s in targets.Cast<SkinnedMeshRenderer>())
            {
                if (s.sharedMesh == null) continue;
                _meshes.Add(s.sharedMesh);
                _transforms.Add(s.transform);
            }

            _debugGizmoManager?.UpdateTargets(_meshes, _transforms);
            _baseSizeFoldoutManager?.UpdateTargets(_meshes);
        }

        void UpdateMeshSelectionWarnings()
        {
            if ((from t in _skinnedMeshRenderers
                    where t.sharedMesh != null
                    let haveClothComponent = t.gameObject.GetComponent<Cloth>() != null
                    where !haveClothComponent && t.sharedMesh.blendShapeCount == 0 &&
                          (t.sharedMesh.boneWeights.Length == 0 ||
                           t.sharedMesh.bindposes.Length == 0)
                    select t).Any())
            {
                _invalidMeshWarning ??= new(
                    "The assigned mesh is missing either bone weights with bind pose, or blend shapes. This might cause the mesh not to render in the Player. If your mesh does not have either bone weights with bind pose, or blend shapes, use a Mesh Renderer instead of Skinned Mesh Renderer.",
                    HelpBoxMessageType.Error);
                _meshField.parent.Insert(1, _invalidMeshWarning);
                return;
            }

            if (_invalidMeshWarning == null) return;
            _meshField.parent.Remove(_invalidMeshWarning);
            _invalidMeshWarning = null;
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

        #endregion MeshField

        #region Blendshapes

        #region Variables

        Toggle _blendShapesFoldoutToggle;
        PropertyField _blendShapesPropertyField;
        bool _changedBlendShapesWithSlider = false;
        HelpBox _legacyClampBlendShapeWeightsInfo;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        List<Slider> _blendShapeSliders = new();

        #endregion Variables

        void SetupBlendShapes()
        {
            _blendShapesFoldout = _root.Q<GroupBox>("BlendShapesFoldout");
            _blendShapesFoldoutToggle = _blendShapesFoldout.Q<Toggle>("FoldoutToggle");
            CustomFoldout.SetupFoldout(_blendShapesFoldout);

            _blendShapesPropertyField = _root.Q<PropertyField>("BlendShapesPropertyField");

            UpdateBlendShapes();

            _blendShapesFoldout.schedule.Execute(UpdateBlendShapesExistingSlidersIfChangedByCode).Every(0)
                .ExecuteLater(0); //1000 ms = 1 s
            _blendShapesPropertyField.schedule.Execute(UpdateBlendShapesPrefabMarkup).Every(1000)
                .ExecuteLater(1000); //1000 ms = 1 s
        }

        void UpdateBlendShapesPrefabMarkup()
        {
            if (_skinnedMeshRenderers.Select(t => t.sharedMesh == null
                    ? 0
                    : t.sharedMesh.blendShapeCount).Any(blendShapeCount => blendShapeCount == 0))
            {
                return;
            }

            if (_blendShapesPropertyField.Q<Toggle>().ClassListContains(PrefabOverrideClass))
                _blendShapesFoldoutToggle.AddToClassList(PrefabOverrideClass);
            else
                _blendShapesFoldoutToggle.RemoveFromClassList(PrefabOverrideClass);

            List<FloatField> floatFields = _blendShapesPropertyField.Query<FloatField>().ToList();

            // ReSharper disable once EqualExpressionComparison
            if (_blendShapeSliders.Count != _blendShapeSliders.Count)
            {
                UpdateBlendShapes();
                return;
            }

            for (int i = 0; i < floatFields.Count; i++)
            {
                if (floatFields[i].ClassListContains(PrefabOverrideClass))
                    _blendShapeSliders[i].AddToClassList(PrefabOverrideClass);
                else
                    _blendShapeSliders[i].RemoveFromClassList(PrefabOverrideClass);
            }
        }

        /// <summary>
        /// This runs every frame to update blend shapes
        /// </summary>
        void UpdateBlendShapesExistingSlidersIfChangedByCode()
        {
            if (_skinnedMeshRenderers.Length > 1) return;

            foreach (SkinnedMeshRenderer t in _skinnedMeshRenderers)
            {
                int blendShapeCount = t.sharedMesh == null
                    ? 0
                    : t.sharedMesh.blendShapeCount;
                if (blendShapeCount == 0)
                {
                    //blendShapesFoldout.style.display = DisplayStyle.None;
                    return;
                }

                if (_blendShapeSliders.Count != blendShapeCount)
                {
                    UpdateBlendShapes();
                    return;
                }

                for (int k = 0; k < blendShapeCount; k++)
                {
                    if (_blendShapeSliders[k] == null)
                    {
                        UpdateBlendShapes();
                        break;
                    }

                    if (Mathf.Approximately(_blendShapeSliders[k].value,
                            t.GetBlendShapeWeight(k)))
                        continue;

                    if (_changedBlendShapesWithSlider)
                        continue;

                    _blendShapeSliders[k].SetValueWithoutNotify(t.GetBlendShapeWeight(k));
                }
            }
        }

        void UpdateBlendShapes()
        {
            if (_skinnedMeshRenderers.Length > 1) return;

            int blendShapeCount = _skinnedMeshRenderers[0].sharedMesh == null
                ? 0
                : _skinnedMeshRenderers[0].sharedMesh.blendShapeCount;
            if (blendShapeCount == 0)
            {
                _blendShapesFoldout.style.display = DisplayStyle.None;
                return;
            }

            if (PlayerSettings.legacyClampBlendShapeWeights)
            {
                _legacyClampBlendShapeWeightsInfo ??= new(
                    "Note that BlendShape weight range is clamped.This can be disabled in Player Settings.",
                    HelpBoxMessageType.Error);
                _blendShapesFoldout.Q<GroupBox>("Content").Insert(0, _legacyClampBlendShapeWeightsInfo);
            }
            else if (_legacyClampBlendShapeWeightsInfo != null)
            {
                _blendShapesFoldout.Q<GroupBox>("Content").Remove(_legacyClampBlendShapeWeightsInfo);
                _legacyClampBlendShapeWeightsInfo = null;
            }

            CreateBlendShapesList(blendShapeCount);
        }

        void CreateBlendShapesList(int blendShapeCount)
        {
            if (_skinnedMeshRenderers.Length > 1) return;

            GroupBox blendShapesList = _blendShapesFoldout.Q<GroupBox>("BlendShapesList");
            blendShapesList.Clear();
            Mesh m = _skinnedMeshRenderers[0].sharedMesh;
            for (int k = 0; k < blendShapeCount; k++)
            {
                int
                    i = k; //Cache the value to avoid referencing the latest value at the time registerValueChanged is triggered
                string blendShapeName = m.GetBlendShapeName(i);

                // Calculate the min and max values for the slider from the frame blendshape weights
                float sliderMin = 0f, sliderMax = 0f;

                int frameCount = m.GetBlendShapeFrameCount(i);
                for (int j = 0; j < frameCount; j++)
                {
                    float frameWeight = m.GetBlendShapeFrameWeight(i, j);
                    sliderMin = Mathf.Min(frameWeight, sliderMin);
                    sliderMax = Mathf.Max(frameWeight, sliderMax);
                }

                // The SkinnedMeshRenderer blendshape weights array size can be out of sync with the size defined in the mesh
                // (default values in that case are 0)
                // The desired behavior is to resize the blendshape array on edit.

                Slider slider = new(sliderMin, sliderMax)
                {
                    label = blendShapeName,
                    showInputField = true
                };
                blendShapesList.Add(slider);
                _blendShapeSliders.Add(slider);

                slider.value = _skinnedMeshRenderers[0].GetBlendShapeWeight(i);
                slider.RegisterValueChangedCallback(_ =>
                {
                    _changedBlendShapesWithSlider = true;
                    _skinnedMeshRenderers[0].SetBlendShapeWeight(i, slider.value);
                });

                //// Default path when the blend shape array size is big enough.
                //if (i < arraySize)
                //{
                //    //EditorGUILayout.Slider(m_BlendShapeWeights.GetArrayElementAtIndex(i), sliderMin, sliderMax, float.MinValue, float.MaxValue, content);
                //}
                //// Fall back to 0 based editing &
                //else
                //{
                //    //    EditorGUI.BeginChangeCheck();

                //    //float value = EditorGUILayout.Slider(content, 0f, sliderMin, sliderMax, float.MinValue, float.MaxValue);
                //    //    if (EditorGUI.EndChangeCheck())
                //    //    {
                //    //        m_BlendShapeWeights.arraySize = blendShapeCount;
                //    //        arraySize = blendShapeCount;
                //    //        m_BlendShapeWeights.GetArrayElementAtIndex(i).floatValue = value;
                //    //    }
                //}
            }
        }

        #endregion Blendshapes

        void SetupAdditionalSettings()
        {
            GroupBox additionalSettingsFoldout = _root.Q<GroupBox>("AdditionalSettingsFoldout");
            CustomFoldout.SetupFoldout(additionalSettingsFoldout);
        }

        void SetupLightingFoldout()
        {
            GroupBox lightingFoldout = _root.Q<GroupBox>("LightningFoldout");
            CustomFoldout.SetupFoldout(lightingFoldout);
        }

        void SetupDebugFoldout()
        {
            GroupBox debugFoldout = _root.Q<GroupBox>("DebugFoldout");
            CustomFoldout.SetupFoldout(debugFoldout);

            GroupBox debugPropertiesFoldout = debugFoldout.Q<GroupBox>("DebugPropertiesFoldout");
            CustomFoldout.SetupFoldout(debugPropertiesFoldout);

            GroupBox content = debugPropertiesFoldout.Q<GroupBox>("Content");

            //Setting content disabled wasn't applying that disabled visual
            //content.SetEnabled(false);
            foreach (VisualElement child in content.Children())
            {
                child.SetEnabled(false);
            }

            GroupBox debugGizmosFoldout = debugFoldout.Q<GroupBox>("MeshDebugFoldout");
            CustomFoldout.SetupFoldout(debugGizmosFoldout);

            UpdateDebugFoldoutVisibility();
        }

        void UpdateDebugFoldoutVisibility()
        {
            _root.Q<GroupBox>("DebugFoldout").style.display =
                _editorSettings.ShowDebugGizmoFoldout ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #region Bounding Probes Volume

        void SetupBoundingVolume()
        {
            GroupBox boundingBoxFoldout = _root.Q<GroupBox>("BoundingBoxFoldout");
            CustomFoldout.SetupFoldout(boundingBoxFoldout);

            Button editBoundingVolumeButton = boundingBoxFoldout.Q<Button>("EditBoundingVolumeButton");
            editBoundingVolumeButton.tooltip = BoundingVolumeEditButtonTooltip;

            if (EditMode.editMode == EditMode.SceneViewEditMode.None)
                editBoundingVolumeButton.RemoveFromClassList("toggledOnButton");
            else
                editBoundingVolumeButton.AddToClassList("toggledOnButton");

            editBoundingVolumeButton.clicked += () =>
            {
                if (EditMode.editMode == EditMode.SceneViewEditMode.None)
                {
                    EditMode.ChangeEditMode(EditMode.SceneViewEditMode.Collider, _skinnedMeshRenderers[0].bounds, this);
                    editBoundingVolumeButton.AddToClassList("toggledOnButton");
                }
                else
                {
                    EditMode.ChangeEditMode(EditMode.SceneViewEditMode.None, _skinnedMeshRenderers[0].bounds, this);
                    editBoundingVolumeButton.RemoveFromClassList("toggledOnButton");
                }
            };

            Button autoFitBoundsButton = boundingBoxFoldout.Q<Button>("AutoFitBoundsButton");
            autoFitBoundsButton.clicked += () =>
            {
                Undo.RecordObject(_skinnedMeshRenderers[0], "Auto-Fit Bounds");
                //targetSkinnedMeshRenderer.localBounds = targetSkinnedMeshRenderer.sharedMesh != null ? targetSkinnedMeshRenderer.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);

                _skinnedMeshRenderers[0].updateWhenOffscreen = true;
                Bounds fitBounds = _skinnedMeshRenderers[0].localBounds;
                _skinnedMeshRenderers[0].updateWhenOffscreen = false;
                _skinnedMeshRenderers[0].localBounds = fitBounds;
            };

            _boundsGroupBox = boundingBoxFoldout.Q<GroupBox>("BoundsGroupBox");
            _aabb = _root.Q<PropertyField>("AABBBindingField");
            Vector3Field boundsCenter = _boundsGroupBox.Q<Vector3Field>("BoundsCenter");
            Vector3Field boundsExtent = _boundsGroupBox.Q<Vector3Field>("BoundsExtentField");
            boundsCenter.SetValueWithoutNotify(_skinnedMeshRenderers[0].localBounds.center);
            boundsExtent.SetValueWithoutNotify(_skinnedMeshRenderers[0].localBounds.extents);
            UpdateBoundsGroupBoxContextMenu();

            _aabb.RegisterValueChangeCallback(_ =>
            {
                boundsCenter.SetValueWithoutNotify(_skinnedMeshRenderers[0].localBounds.center);
                boundsExtent.SetValueWithoutNotify(_skinnedMeshRenderers[0].localBounds.extents);
                _aabb.schedule.Execute(UpdateBoundsFieldPrefabOverride).ExecuteLater(1000); //1000 ms = 1 s
                UpdateBoundsGroupBoxContextMenu();
            });

            boundsCenter.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_skinnedMeshRenderers[0],
                    "Modified bounds center in " + _skinnedMeshRenderers[0].gameObject.name);
                Bounds bounds = _skinnedMeshRenderers[0].localBounds;
                bounds.center = e.newValue;
                _skinnedMeshRenderers[0].localBounds = bounds;
                EditorUtility.SetDirty(_skinnedMeshRenderers[0]);
            });
            boundsExtent.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_skinnedMeshRenderers[0],
                    "Modified bounds extents in " + _skinnedMeshRenderers[0].gameObject.name);
                Bounds bounds = _skinnedMeshRenderers[0].localBounds;
                bounds.extents = e.newValue;
                _skinnedMeshRenderers[0].localBounds = bounds;
                EditorUtility.SetDirty(_skinnedMeshRenderers[0]);
            });
            _aabb.schedule.Execute(UpdateBoundsFieldPrefabOverride).ExecuteLater(1000); //1000 ms = 1 s
        }

        ContextualMenuManipulator _contextualMenuManipulatorForBoundsGroupBox;

        /// <summary>
        /// The right click menu
        /// </summary>
        void UpdateBoundsGroupBoxContextMenu()
        {
            //Remove the old context menu
            if (_contextualMenuManipulatorForBoundsGroupBox != null)
                _boundsGroupBox.RemoveManipulator(_contextualMenuManipulatorForBoundsGroupBox);

            if (targets.Length > 1) return;

            UpdateContextMenuForBoundsGroupBox();

            _boundsGroupBox.AddManipulator(_contextualMenuManipulatorForBoundsGroupBox);
            return;

            void UpdateContextMenuForBoundsGroupBox()
            {
                _contextualMenuManipulatorForBoundsGroupBox = new(evt =>
                {
                    evt.menu.AppendAction("Copy property path", _ => CopyBoundsPropertyPath(),
                        DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendSeparator();

                    if (HasPrefabOverrideBounds())
                    {
                        GameObject prefab =
                            PrefabUtility.GetOutermostPrefabInstanceRoot(_skinnedMeshRenderers[0].gameObject);
                        string prefabName = prefab ? " to '" + prefab.name + "'" : "";

                        evt.menu.AppendAction("Apply to Prefab" + prefabName, _ => ApplyChangesToPrefabBounds(),
                            DropdownMenuAction.AlwaysEnabled);
                        evt.menu.AppendAction("Revert", _ => RevertChangesBounds(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Copy", _ => Copy(), DropdownMenuAction.AlwaysEnabled);

                    GetBoundsFromCopyBuffer(out bool exists, out _, out _, out _, out _,
                        out _, out _);

                    if (exists)
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysDisabled);
                });
            }

            void CopyBoundsPropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = "m_AABB";
            }

            void Copy()
            {
                Bounds b = _skinnedMeshRenderers[0].localBounds;
                const char valueSeparators = ',';
                EditorGUIUtility.systemCopyBuffer = "Bounds(" + b.center.x + valueSeparators + b.center.y +
                                                    valueSeparators + b.center.z + valueSeparators + b.extents.x +
                                                    valueSeparators + b.extents.y + valueSeparators + b.extents.z + ")";
            }

            void Paste()
            {
                GetBoundsFromCopyBuffer(out bool exists, out float x, out float y, out float z, out float a,
                    out float b, out float c);
                if (!exists) return;

                _skinnedMeshRenderers[0].localBounds = new(new(x, y, z), new(a, b, c));
            }

            bool HasPrefabOverrideBounds()
            {
                SerializedObject soTarget = new(target);
                return soTarget.FindProperty("m_AABB").prefabOverride;
            }

            void ApplyChangesToPrefabBounds()
            {
                if (!HasPrefabOverrideBounds())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty("m_AABB"),
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_skinnedMeshRenderers[0].transform),
                    InteractionMode.UserAction);
            }

            void RevertChangesBounds()
            {
                if (!HasPrefabOverrideBounds())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.RevertPropertyOverride(soTarget.FindProperty("m_AABB"), InteractionMode.UserAction);
            }
        }

        //TODO Refactor this code.
        static void GetBoundsFromCopyBuffer(out bool exists, out float x, out float y, out float z, out float a,
            out float b, out float c)
        {
            exists = false;
            x = 0;
            y = 0;
            z = 0;
            a = 0;
            b = 0;
            c = 0;

            string copyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (copyBuffer != null)
            {
                if (copyBuffer.Contains("Bounds"))
                {
                    if (copyBuffer.Length > 17)
                    {
                        copyBuffer = copyBuffer.Substring(7, copyBuffer.Length - 8);
                        string[] valueStrings = copyBuffer.Split(',');

                        if (valueStrings.Length != 6) return;
                        char userDecimalSeparator =
                            Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                        string sanitizedValueStringX = valueStrings[0]
                            .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                        if (float.TryParse(sanitizedValueStringX, NumberStyles.Float, CultureInfo.CurrentCulture,
                                out x))
                            exists = true;

                        if (!exists) return;
                        {
                            string sanitizedValueStringY = valueStrings[1]
                                .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (!float.TryParse(sanitizedValueStringY, NumberStyles.Float,
                                    CultureInfo.CurrentCulture, out y))
                                exists = false;
                        }

                        if (exists)
                        {
                            string sanitizedValueStringZ = valueStrings[2]
                                .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (!float.TryParse(sanitizedValueStringZ, NumberStyles.Float,
                                    CultureInfo.CurrentCulture, out z))
                                exists = false;
                        }

                        if (exists)
                        {
                            string sanitizedValueStringZ = valueStrings[3]
                                .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (!float.TryParse(sanitizedValueStringZ, NumberStyles.Float,
                                    CultureInfo.CurrentCulture, out a))
                                exists = false;
                        }

                        if (exists)
                        {
                            string sanitizedValueStringZ = valueStrings[4]
                                .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (!float.TryParse(sanitizedValueStringZ, NumberStyles.Float,
                                    CultureInfo.CurrentCulture, out b))
                                exists = false;
                        }

                        if (!exists) return;
                        {
                            string sanitizedValueStringZ = valueStrings[5]
                                .Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (!float.TryParse(sanitizedValueStringZ, NumberStyles.Float,
                                    CultureInfo.CurrentCulture, out x))
                                exists = false;
                        }
                    }
                }
            }
        }

        void UpdateBoundsFieldPrefabOverride()
        {
            Label label = _aabb.Q<Label>();
            if (label == null) return;

            if (label.ClassListContains(PrefabOverrideClass))
                _boundsGroupBox.AddToClassList(PrefabOverrideClass);
            else
                _boundsGroupBox.RemoveFromClassList(PrefabOverrideClass);
        }

        #region Probes Foldout

        IntegerField _lightProbeBindingField;
        EnumField _lightProbeEnumField;
        PropertyField _lightProbeVolumeOverrideField;
        ObjectField _anchorOverrideField;
        HelpBox _invalidLightProbeWarning;

        IntegerField _reflectionProbeBindingField;
        EnumField _reflectionProbeEnumField;

        const string PrefabOverrideClass = "unity-binding--prefab-override";

        void SetupProbesFoldout()
        {
            GroupBox probesFoldout = _root.Q<GroupBox>("ProbesFoldout");
            CustomFoldout.SetupFoldout(probesFoldout);

            _lightProbeBindingField = probesFoldout.Q<IntegerField>("LightProbeBindingField");
            _lightProbeEnumField = probesFoldout.Q<EnumField>("LightProbeEnumField");
            _lightProbeEnumField.SetValueWithoutNotify(_skinnedMeshRenderers[0].lightProbeUsage);

            _lightProbeVolumeOverrideField = probesFoldout.Q<PropertyField>("LightProbeVolumeOverrideField");
            _anchorOverrideField = probesFoldout.Q<ObjectField>("AnchorOverrideField");

            _lightProbeEnumField.RegisterValueChangedCallback(e =>
            {
                _skinnedMeshRenderers[0].lightProbeUsage = (LightProbeUsage)e.newValue;
            });

            _lightProbeEnumField.schedule.Execute(RegisterLightProbe).ExecuteLater(0);

            _lightProbeBindingField.schedule.Execute(UpdateLightingProbePrefabOverride)
                .ExecuteLater(1000); //1000 ms = 1 s

            _lightProbeVolumeOverrideField.RegisterValueChangeCallback(_ => { UpdateLightProbeProxyVolumeWarning(); });

            UpdateAnchorFieldVisibility();
            UpdateLightProbeProxyVolumeWarning();

            UpdateLightProbeContextMenu();

            _reflectionProbeBindingField = probesFoldout.Q<IntegerField>("ReflectionProbeBindingField");
            _reflectionProbeEnumField = probesFoldout.Q<EnumField>("ReflectionProbeEnumField");
            _reflectionProbeEnumField.SetValueWithoutNotify(_skinnedMeshRenderers[0].reflectionProbeUsage);

            UpdateReflectionProbeContextMenu();

            _reflectionProbeEnumField.RegisterValueChangedCallback(e =>
            {
                _skinnedMeshRenderers[0].reflectionProbeUsage =
                    (ReflectionProbeUsage)e.newValue;
            });

            _lightProbeEnumField.schedule.Execute(RegisterReflectionProbe).ExecuteLater(0);
            _reflectionProbeBindingField.schedule.Execute(UpdateReflectionProbePrefabOverride)
                .ExecuteLater(1); //1000 ms = 1 s
            _reflectionProbeBindingField.schedule.Execute(UpdateReflectionProbePrefabOverride)
                .ExecuteLater(1000); //1000 ms = 1 s
        }

        void RegisterReflectionProbe()
        {
            _reflectionProbeBindingField.RegisterValueChangedCallback(_ =>
            {
                UpdateAnchorFieldVisibility();
                _reflectionProbeEnumField.SetValueWithoutNotify(_skinnedMeshRenderers[0].reflectionProbeUsage);
                _reflectionProbeBindingField.schedule.Execute(UpdateReflectionProbePrefabOverride)
                    .ExecuteLater(500); //1000 ms = 1 s
                _reflectionProbeBindingField.schedule.Execute(UpdateReflectionProbePrefabOverride)
                    .ExecuteLater(2000); //1000 ms = 1 s
                _reflectionProbeBindingField.schedule.Execute(UpdateReflectionProbePrefabOverride)
                    .ExecuteLater(10000); //1000 ms = 1 s //Seems unnecessary but was required one time.
                UpdateReflectionProbeContextMenu();
            });
        }

        void RegisterLightProbe()
        {
            _lightProbeBindingField.RegisterValueChangedCallback(_ =>
            {
                UpdateAnchorFieldVisibility();
                _lightProbeEnumField.SetValueWithoutNotify(_skinnedMeshRenderers[0].lightProbeUsage);
                UpdateLightProbeProxyVolumeWarning();
                _lightProbeBindingField.schedule.Execute(UpdateLightingProbePrefabOverride)
                    .ExecuteLater(500); //1000 ms = 1 s
                _lightProbeBindingField.schedule.Execute(UpdateLightingProbePrefabOverride)
                    .ExecuteLater(2000); //1000 ms = 1 s
                UpdateLightProbeContextMenu();
            });
        }

        ContextualMenuManipulator _contextualMenuManipulatorForLightProbeEnumField;

        /// <summary>
        /// The right click menu
        /// </summary>
        void UpdateLightProbeContextMenu()
        {
            //Remove the old context menu
            if (_contextualMenuManipulatorForLightProbeEnumField != null)
                _lightProbeEnumField.RemoveManipulator(_contextualMenuManipulatorForLightProbeEnumField);

            if (targets.Length > 1) return;

            UpdateContextMenuForLightProbeField();

            _lightProbeEnumField.AddManipulator(_contextualMenuManipulatorForLightProbeEnumField);
            return;

            void UpdateContextMenuForLightProbeField()
            {
                _contextualMenuManipulatorForLightProbeEnumField = new(evt =>
                {
                    evt.menu.AppendAction("Copy property path", _ => CopyPropertyPath(),
                        DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendSeparator();

                    if (HasPrefabOverrideLightProbe())
                    {
                        GameObject prefab =
                            PrefabUtility.GetOutermostPrefabInstanceRoot(_skinnedMeshRenderers[0].gameObject);
                        string prefabName = prefab ? " to '" + prefab.name + "'" : "";

                        evt.menu.AppendAction("Apply to Prefab" + prefabName, _ => ApplyChangesToPrefabLightProbe(),
                            DropdownMenuAction.AlwaysEnabled);
                        evt.menu.AppendAction("Revert", _ => RevertChangesLightProbe(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Copy", _ => Copy(), DropdownMenuAction.AlwaysEnabled);

                    if (HasValidValueForPaste())
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysDisabled);
                });
            }

            void CopyPropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = "m_LightProbeUsage";
            }

            void Copy()
            {
                EditorGUIUtility.systemCopyBuffer = ((int)_skinnedMeshRenderers[0].lightProbeUsage).ToString();
            }

            void Paste()
            {
                if (!HasValidValueForPaste()) return;
                string value = EditorGUIUtility.systemCopyBuffer;
                if (value == null) return;

                if (!int.TryParse(value, out int intValue)) return;
                if (Enum.IsDefined(typeof(LightProbeUsage), intValue))
                {
                    _skinnedMeshRenderers[0].lightProbeUsage = (LightProbeUsage)intValue;
                }
            }

            bool HasValidValueForPaste() => EditorGUIUtility.systemCopyBuffer != null &&
                                            IsValidEnumValue<LightProbeUsage>(EditorGUIUtility.systemCopyBuffer);

            bool HasPrefabOverrideLightProbe()
            {
                SerializedObject soTarget = new(target);
                return soTarget.FindProperty("m_LightProbeUsage").prefabOverride;
            }

            void ApplyChangesToPrefabLightProbe()
            {
                if (!HasPrefabOverrideLightProbe())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty("m_LightProbeUsage"),
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_skinnedMeshRenderers[0].transform),
                    InteractionMode.UserAction);
            }

            void RevertChangesLightProbe()
            {
                if (!HasPrefabOverrideLightProbe())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.RevertPropertyOverride(soTarget.FindProperty("m_LightProbeUsage"),
                    InteractionMode.UserAction);
            }
        }

        ContextualMenuManipulator _contextualMenuManipulatorForReflectionProbeEnumField;

        /// The right click menu
        void UpdateReflectionProbeContextMenu()
        {
            //Remove the old context menu
            if (_contextualMenuManipulatorForReflectionProbeEnumField != null)
                _reflectionProbeEnumField.RemoveManipulator(_contextualMenuManipulatorForReflectionProbeEnumField);

            if (targets.Length > 1) return;

            UpdateContextMenuForReflectionProbeField();

            _reflectionProbeEnumField.AddManipulator(_contextualMenuManipulatorForReflectionProbeEnumField);

            return;

            void UpdateContextMenuForReflectionProbeField()
            {
                _contextualMenuManipulatorForReflectionProbeEnumField = new(evt =>
                {
                    evt.menu.AppendAction("Copy property path", _ => CopyPropertyPath(),
                        DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendSeparator();

                    if (HasPrefabOverrideReflectionProbe())
                    {
                        GameObject prefab =
                            PrefabUtility.GetOutermostPrefabInstanceRoot(_skinnedMeshRenderers[0].gameObject);
                        string prefabName = prefab ? " to '" + prefab.name + "'" : "";

                        evt.menu.AppendAction("Apply to Prefab" + prefabName,
                            _ => ApplyChangesToPrefabReflectionProbe(), DropdownMenuAction.AlwaysEnabled);
                        evt.menu.AppendAction("Revert", _ => RevertChangesReflectionProbe(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Copy", _ => Copy(), DropdownMenuAction.AlwaysEnabled);

                    if (HasValidValueForPaste())
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", _ => Paste(), DropdownMenuAction.AlwaysDisabled);
                });
            }

            void CopyPropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = "m_ReflectionProbeUsage";
            }

            void Copy()
            {
                EditorGUIUtility.systemCopyBuffer = ((int)_skinnedMeshRenderers[0].reflectionProbeUsage).ToString();
            }

            void Paste()
            {
                if (!HasValidValueForPaste()) return;
                string value = EditorGUIUtility.systemCopyBuffer;
                if (value == null) return;

                if (!int.TryParse(value, out int intValue)) return;
                if (Enum.IsDefined(typeof(ReflectionProbeUsage), intValue))
                {
                    _skinnedMeshRenderers[0].reflectionProbeUsage =
                        (ReflectionProbeUsage)intValue;
                }
            }

            bool HasValidValueForPaste()
            {
                return EditorGUIUtility.systemCopyBuffer != null &&
                       IsValidEnumValue<ReflectionProbeUsage>(EditorGUIUtility.systemCopyBuffer);
            }

            bool HasPrefabOverrideReflectionProbe()
            {
                SerializedObject soTarget = new(target);
                return soTarget.FindProperty("m_ReflectionProbeUsage").prefabOverride;
            }

            void ApplyChangesToPrefabReflectionProbe()
            {
                if (!HasPrefabOverrideReflectionProbe())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty("m_ReflectionProbeUsage"),
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_skinnedMeshRenderers[0].transform),
                    InteractionMode.UserAction);
            }

            void RevertChangesReflectionProbe()
            {
                if (!HasPrefabOverrideReflectionProbe())
                    return;

                SerializedObject soTarget = new(target);
                PrefabUtility.RevertPropertyOverride(soTarget.FindProperty("m_ReflectionProbeUsage"),
                    InteractionMode.UserAction);
            }
        }

        void UpdateLightingProbePrefabOverride()
        {
            if (_lightProbeBindingField.ClassListContains(PrefabOverrideClass))
                _lightProbeEnumField.AddToClassList(PrefabOverrideClass);
            else
                _lightProbeEnumField.RemoveFromClassList(PrefabOverrideClass);
        }

        void UpdateReflectionProbePrefabOverride()
        {
            if (_reflectionProbeBindingField.ClassListContains(PrefabOverrideClass))
                _reflectionProbeEnumField.AddToClassList(PrefabOverrideClass);
            else
                _reflectionProbeEnumField.RemoveFromClassList(PrefabOverrideClass);
        }

        void UpdateAnchorFieldVisibility()
        {
            if (_skinnedMeshRenderers[0].lightProbeUsage == LightProbeUsage.Off &&
                _skinnedMeshRenderers[0].reflectionProbeUsage == ReflectionProbeUsage.Off)
                _anchorOverrideField.style.display = DisplayStyle.None;
            else
                _anchorOverrideField.style.display = DisplayStyle.Flex;

            _lightProbeVolumeOverrideField.style.display =
                _skinnedMeshRenderers[0].lightProbeUsage == LightProbeUsage.UseProxyVolume
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        void UpdateLightProbeProxyVolumeWarning()
        {
            if (_skinnedMeshRenderers[0].lightProbeUsage == LightProbeUsage.UseProxyVolume
                && (_skinnedMeshRenderers[0].lightProbeProxyVolumeOverride == null
                    || _skinnedMeshRenderers[0].lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() ==
                    null))
            {
                if (_invalidLightProbeWarning is { parent: not null })
                    _invalidLightProbeWarning.parent.Remove(_invalidLightProbeWarning);

                _invalidLightProbeWarning = new("A valid Light Probe Proxy Volume component could not be found",
                    HelpBoxMessageType.Warning);
                _lightProbeVolumeOverrideField.parent.Insert(
                    _lightProbeVolumeOverrideField.parent.IndexOf(_lightProbeVolumeOverrideField) + 1,
                    _invalidLightProbeWarning);
            }
            else
            {
                if (_invalidLightProbeWarning == null) return;
                _invalidLightProbeWarning.parent?.Remove(_invalidLightProbeWarning);
                _invalidLightProbeWarning = null;
            }
            //lightProbeVolumeOverrideField
        }

        #endregion Probes Foldout

        #endregion Bounding Probes Volume

        #region UI

        void UpdateMeshFieldGroupPosition()
        {
            GroupBox meshFieldGroupBox = _root.Q<GroupBox>("MeshFieldGroupBox");
            if (_editorSettings.meshFieldOnTop)
                _root.Q<GroupBox>("RootHolder").Insert(2, meshFieldGroupBox);
            else
                _root.Q<VisualElement>("MainContainer").Insert(0, meshFieldGroupBox);
        }

        #endregion UI

        /// <summary>
        /// If the UXML file is missing for any reason,
        /// Instead of showing an empty inspector,
        /// This loads the default one.
        /// This shouldn't ever happen.
        /// </summary>
        void LoadDefaultEditor(VisualElement container)
        {
            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);

            //originalEditor = Editor.CreateEditor(targets);
            _originalEditor = CreateEditor(targets,
                typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor"));
            IMGUIContainer inspectorContainer = new(OnGUICallback);
            container.Add(inspectorContainer);
        }

        //For the original Editor
        void OnGUICallback()
        {
            //EditorGUIUtility.hierarchyMode = true;

            EditorGUI.BeginChangeCheck();
            _originalEditor.OnInspectorGUI();
            EditorGUI.EndChangeCheck();
        }

        void CleanUp()
        {
            _previewManager?.CleanUp();

            // Clean up
            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);

            _debugGizmoManager?.Cleanup();
        }

        #region Settings

        void OpenContextMenu_settingsButton()
        {
            UpdateContextMenu_settingsButton();
            _settingsButtonContextMenu.DropDown(GetMenuRect(_settingsButton), _settingsButton, true);
        }

        void UpdateContextMenu_settingsButton()
        {
            _settingsButtonContextMenu = new();

            if (_editorSettings.showDefaultSkinnedMeshRendererInspector)
                _settingsButtonContextMenu.AddItem("Default Inspector", true, () =>
                {
                    _editorSettings.showDefaultSkinnedMeshRendererInspector = false;
                    UpdateInspectorOverride();
                });
            else
                _settingsButtonContextMenu.AddItem("Default Inspector", false, () =>
                {
                    _editorSettings.showDefaultSkinnedMeshRendererInspector = true;
                    UpdateInspectorOverride();
                });

            _settingsButtonContextMenu.AddSeparator("");

            bool isChecked = !_settingsFoldoutManager.IsInspectorSettingsIsHidden();
            _settingsButtonContextMenu.AddItem("Open Settings", isChecked,
                () => { _settingsFoldoutManager.ToggleInspectorSettings(); });
        }

        static Rect GetMenuRect(VisualElement anchor)
        {
            Rect worldBound = anchor.worldBound;
            worldBound.xMin -= 150;
            worldBound.xMax += 0;
            return worldBound;
        }

        #endregion Settings

        static bool IsValidEnumValue<TEnum>(string input) where TEnum : struct, Enum
        {
            return int.TryParse(input, out int intValue) && Enum.IsDefined(typeof(TEnum), intValue);
        }

        #region Gizmo

        readonly BoxBoundsHandle _mBoundsHandle = new();

        void OnSceneGUI()
        {
            if (!target)
                return;

            SkinnedMeshRenderer renderer = (SkinnedMeshRenderer)target;

            if (renderer.updateWhenOffscreen)
            {
                Bounds bounds = renderer.bounds;
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;

                Handles.DrawWireCube(center, size);
            }
            else
            {
                //using (new Handles.DrawingScope(renderer.actualRootBone.localToWorldMatrix))
                using (new Handles.DrawingScope(renderer.rootBone.localToWorldMatrix))
                {
                    Bounds bounds = renderer.localBounds;
                    _mBoundsHandle.center = bounds.center;
                    _mBoundsHandle.size = bounds.size;

                    // only display interactive handles if edit mode is active
                    if (EditMode.editMode == EditMode.SceneViewEditMode.Collider)
                    {
                        Handles.color = new(0, 0.75f, 1, 1f);
                        _mBoundsHandle.wireframeColor = Color.white;
                        _mBoundsHandle.handleColor = _mBoundsHandle.wireframeColor;

                        EditorGUI.BeginChangeCheck();
                        _mBoundsHandle.DrawHandle();
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(renderer, "Resize Bounds");
                            renderer.localBounds = new(_mBoundsHandle.center, _mBoundsHandle.size);
                        }

                        Handles.color = new(0, 0.75f, 1, 0.05f);
                        float pulse = 1 + Mathf.Sin((float)EditorApplication.timeSinceStartup * 5f) * 0.05f;
                        Vector3 pulseSize = _mBoundsHandle.size * pulse;
                        Handles.DrawWireCube(_mBoundsHandle.center, pulseSize);

                        Handles.color = new(0, 0.75f, 1, 0.2f);
                        Handles.DrawWireCube(_mBoundsHandle.center,
                            _mBoundsHandle.size *
                            (1 + Mathf.Sin((float)EditorApplication.timeSinceStartup * 5f) * 0.025f));

                        SceneView.RepaintAll(); // force refresh
                    }
                    else
                    {
                        _mBoundsHandle.wireframeColor = new(1, 1, 1, 0.5f);
                        _mBoundsHandle.handleColor = Color.clear;
                        _mBoundsHandle.DrawHandle();
                    }
                }
            }


            _debugGizmoManager?.DrawGizmo(_skinnedMeshRenderers);
        }

        #endregion Gizmo
    }
}