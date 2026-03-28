using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    public class ActionsFoldoutManager
    {
        readonly GroupBox _container;

        GroupBox _lightmapGenerateFoldout;
        SliderInt _lightmapGenerateHardAngle;
        SliderInt _lightmapGenerateAngleError;
        SliderInt _lightmapGenerateAreaError;
        SliderInt _lightmapGeneratePackMargin;

        readonly BetterMeshSettings _editorSettings;

        readonly List<MeshFilter> _sourceMeshFilters = new();
        readonly List<SkinnedMeshRenderer> _sourceSkinnedMeshRenderers = new();

        public ActionsFoldoutManager(BetterMeshSettings editorSettings, VisualElement root, Object[] targets)
        {
            _sourceMeshFilters ??= new();
            _sourceSkinnedMeshRenderers ??= new();

            foreach (Object target in targets)
            {
                if (target == null) continue;
                if (target as MeshFilter != null) _sourceMeshFilters.Add(target as MeshFilter);
                if (target as SkinnedMeshRenderer != null)
                    _sourceSkinnedMeshRenderers.Add(target as SkinnedMeshRenderer);
            }

            _editorSettings = editorSettings;

            _container = root.Q<GroupBox>("Buttons");
            CustomFoldout.SetupFoldout(_container);

            UpdateDisplayStyle();

            SetupLightmapGenerateFoldout();

            Toggle doNotApplyActionToAssetToggle = root.Q<Toggle>("doNotApplyActionToAsset");
            doNotApplyActionToAssetToggle.value = editorSettings.DoNotApplyActionToAsset;
            doNotApplyActionToAssetToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.DoNotApplyActionToAsset = ev.newValue;
            });

            Button optimizeMeshButton = root.Q<Button>("OptimizeMesh");
            optimizeMeshButton.tooltip = OptimizeMeshTooltip();
            optimizeMeshButton.style.display =
                editorSettings.ShowOptimizeButton ? DisplayStyle.Flex : DisplayStyle.None;
            optimizeMeshButton.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    OptimizeMesh(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    OptimizeMesh(i);
                }
            };

            Button recalculateNormals = root.Q<Button>("RecalculateNormals");
            recalculateNormals.style.display =
                editorSettings.ShowRecalculateNormalsButton ? DisplayStyle.Flex : DisplayStyle.None;
            recalculateNormals.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    RecalculateNormals(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    RecalculateNormals(i);
                }
            };

            Button recalculateTangents = root.Q<Button>("RecalculateTangents");
            recalculateTangents.style.display =
                editorSettings.ShowRecalculateTangentsButton ? DisplayStyle.Flex : DisplayStyle.None;
            recalculateTangents.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    RecalculateTangents(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    RecalculateTangents(i);
                }
            };

            Button flipNormals = root.Q<Button>("FlipNormals");
            flipNormals.style.display = editorSettings.ShowFlipNormalsButton ? DisplayStyle.Flex : DisplayStyle.None;
            flipNormals.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    FlipNormals(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    FlipNormals(i);
                }
            };

            Button generateSecondaryUVSet = root.Q<Button>("GenerateSecondaryUVSet");
            generateSecondaryUVSet.style.display =
                editorSettings.ShowGenerateSecondaryUVButton ? DisplayStyle.Flex : DisplayStyle.None;
            generateSecondaryUVSet.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    GenerateSecondaryUVSet(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ConvertMeshToInstanceIfRequired(i);
                    GenerateSecondaryUVSet(i);
                }
            };

            Button saveMeshAsField = root.Q<Button>("exportMesh");
            saveMeshAsField.style.display = editorSettings.ShowSaveMeshAsButton ? DisplayStyle.Flex : DisplayStyle.None;
            saveMeshAsField.clicked += () =>
            {
                for (int i = 0; i < _sourceMeshFilters.Count; i++)
                {
                    ExportMesh(i);
                }

                for (int i = 0; i < _sourceSkinnedMeshRenderers.Count; i++)
                {
                    ExportMesh(i);
                }
            };
            return;

            //This is used to make sure the mesh you are modifying is an instance and the user isn't accidentally modifying the asset
            void ConvertMeshToInstanceIfRequired(int index)
            {
                Mesh mesh = _sourceMeshFilters.Count > index
                    ? _sourceMeshFilters[index].sharedMesh
                    : _sourceSkinnedMeshRenderers[index].sharedMesh;

                if (mesh == null) return;

                if (!MeshIsAnAsset(mesh) || !editorSettings.DoNotApplyActionToAsset) return;
                Mesh newMesh = new()
                {
                    vertices = mesh.vertices,
                    triangles = mesh.triangles,
                    uv = mesh.uv,
                    uv2 = mesh.uv2,
                    uv3 = mesh.uv3,
                    uv4 = mesh.uv4,
                    colors = mesh.colors,
                    colors32 = mesh.colors32,
                    boneWeights = mesh.boneWeights,
                    bindposes = mesh.bindposes,
                    normals = mesh.normals,
                    tangents = mesh.tangents,
                    name = mesh.name + " (Local Instance)",
                    subMeshCount = mesh.subMeshCount
                };

                for (int i = 0; i < mesh.subMeshCount; i++)
                    newMesh.SetTriangles(mesh.GetTriangles(i), i);

                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    string shapeName = mesh.GetBlendShapeName(shapeIndex);
                    int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);

                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        float weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                        mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals,
                            deltaTangents);

                        newMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }

                newMesh.RecalculateBounds();

                if (_sourceMeshFilters.Count > index)
                {
                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh instance creation");
                    _sourceMeshFilters[index].sharedMesh = newMesh;
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh instance creation");
                    _sourceSkinnedMeshRenderers[index].sharedMesh = newMesh;
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }
            }

            //void SubDivideMesh(ClickEvent evt)
            //{
            //    sourceMeshFilter.sharedMesh = mesh.SubDivide();
            //    mesh = sourceMeshFilter.sharedMesh;
            //    EditorUtility.SetDirty(mesh);
            //    Log("exported");
            //    UpdateFoldouts(mesh);
            //    SceneView.RepaintAll();
            //}

            void OptimizeMesh(int index)
            {
                if (_sourceMeshFilters.Count > index)
                {
                    if (!_sourceMeshFilters[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh Optimized.");
                    _sourceMeshFilters[index].sharedMesh.Optimize();
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh Optimized.");
                    _sourceSkinnedMeshRenderers[index].sharedMesh.Optimize();
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }

                Debug.Log("Mesh optimized.");
            }

            void RecalculateNormals(int index)
            {
                if (_sourceMeshFilters.Count > index)
                {
                    if (!_sourceMeshFilters[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh Normals Modified.");
                    _sourceMeshFilters[index].sharedMesh.RecalculateNormals();
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh Normals Modified.");
                    _sourceSkinnedMeshRenderers[index].sharedMesh.RecalculateNormals();
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }

                Debug.Log("Mesh Normals recalculated");
            }

            void RecalculateTangents(int index)
            {
                if (_sourceMeshFilters.Count > index)
                {
                    if (!_sourceMeshFilters[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh Tangents Modified.");
                    _sourceMeshFilters[index].sharedMesh.RecalculateTangents();
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh Tangents Modified.");
                    _sourceSkinnedMeshRenderers[index].sharedMesh.RecalculateTangents();
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }

                Debug.Log("Mesh Tangents recalculated");
            }

            void FlipNormals(int index)
            {
                if (_sourceMeshFilters.Count > index)
                {
                    if (!_sourceMeshFilters[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh Normals Flipped.");
                    _sourceMeshFilters[index].sharedMesh.FlipNormals();
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh Normals Flipped.");
                    _sourceSkinnedMeshRenderers[index].sharedMesh.FlipNormals();
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }

                Debug.Log("Mesh Normals Flipped");
                SceneView.RepaintAll();
            }

            void ExportMesh(int index)
            {
                if (_sourceMeshFilters.Count > index)
                {
                    if (!_sourceMeshFilters[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceMeshFilters[index], "Mesh Exported.");
                    _sourceMeshFilters[index].sharedMesh = _sourceMeshFilters[index].sharedMesh.ExportMesh();
                    EditorUtility.SetDirty(_sourceMeshFilters[index]);
                }
                else
                {
                    if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                        return;

                    Undo.RecordObject(_sourceSkinnedMeshRenderers[index], "Mesh Exported.");
                    _sourceSkinnedMeshRenderers[index].sharedMesh =
                        _sourceSkinnedMeshRenderers[index].sharedMesh.ExportMesh();
                    EditorUtility.SetDirty(_sourceSkinnedMeshRenderers[index]);
                }

                Debug.Log("Mesh Exported");
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Turns on and off the foldout
        /// </summary>
        void UpdateDisplayStyle()
        {
            _container.style.display = _editorSettings.ShowActionsFoldout ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetupLightmapGenerateFoldout()
        {
            _lightmapGenerateFoldout = _container.Q<GroupBox>("GenerateSecondaryUVsetGroupBox");
            CustomFoldout.SetupFoldout(_lightmapGenerateFoldout);

            _lightmapGenerateFoldout.style.display =
                _editorSettings.ShowGenerateSecondaryUVButton ? DisplayStyle.Flex : DisplayStyle.None;


            _lightmapGenerateHardAngle = _lightmapGenerateFoldout.Q<SliderInt>("HardAngleSlider");
            _lightmapGenerateAngleError = _lightmapGenerateFoldout.Q<SliderInt>("AngleErrorSlider");
            _lightmapGenerateAreaError = _lightmapGenerateFoldout.Q<SliderInt>("AreaErrorSlider");
            _lightmapGeneratePackMargin = _lightmapGenerateFoldout.Q<SliderInt>("PackMarginSlider");

            _lightmapGenerateFoldout.Q<Button>("ResetGenerateSecondaryUVSetButton").clicked += () =>
            {
                UnwrapParam.SetDefaults(out UnwrapParam unwrapParam);

                _lightmapGenerateHardAngle.value = Mathf.CeilToInt(unwrapParam.hardAngle);
                _lightmapGenerateAngleError.value = Mathf.CeilToInt(Remap(unwrapParam.angleError, 0, 1, 1, 75));
                _lightmapGenerateAreaError.value = Mathf.CeilToInt(Remap(unwrapParam.areaError, 0, 1, 1, 75));
                _lightmapGeneratePackMargin.value = Mathf.CeilToInt(Remap(unwrapParam.packMargin, 0, 1, 1, 64));
            };
        }

        public void MeshUpdated()
        {
            if (!_editorSettings.ShowActionsFoldout)
            {
                _container.style.display = DisplayStyle.None;
                return;
            }

            if (_sourceMeshFilters.Count > 0)
            {
                if (_sourceMeshFilters.Any(meshFilter => meshFilter.sharedMesh != null))
                {
                    _container.style.display = DisplayStyle.Flex;
                    return;
                }
            }
            else
            {
                if (_sourceSkinnedMeshRenderers.Any(t => t.sharedMesh != null))
                {
                    _container.style.display = DisplayStyle.Flex;
                    return;
                }
            }

            _container.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Compute a unique UV layout for a Mesh, and store it in Mesh.uv2.
        /// When you import a model asset, you can instruct Unity to compute a light map UV layout for it using [[ModelImporter-generateSecondaryUV]] or the Model Import Settings Inspector. This function allows you to do the same to procedurally generated meshes.
        ///If this process requires multiple UV charts to flatten the mesh, the mesh might contain more vertices than before. If the mesh uses 16-bit indices (see Mesh.indexFormat) and the process would result in more vertices than are possible to use with 16-bit indices, this function fails and returns false.
        /// Note: Editor only
        /// </summary>
        void GenerateSecondaryUVSet(int index)
        {
            Mesh mesh;

            if (_sourceMeshFilters.Count > index)
            {
                if (!_sourceMeshFilters[index].sharedMesh)
                    return;

                mesh = _sourceMeshFilters[index].sharedMesh;
            }
            else
            {
                if (!_sourceSkinnedMeshRenderers[index].sharedMesh)
                    return;

                mesh = _sourceSkinnedMeshRenderers[index].sharedMesh;
            }

            UnwrapParam.SetDefaults(out UnwrapParam unwrapParam);

            unwrapParam.hardAngle = _lightmapGenerateHardAngle.value;
            unwrapParam.angleError = Remap(_lightmapGenerateAngleError.value, 1f, 75f, 0f, 1f);
            unwrapParam.areaError = Remap(_lightmapGenerateAreaError.value, 1f, 75f, 0f, 1f);
            unwrapParam.packMargin = Remap(_lightmapGeneratePackMargin.value, 0f, 1f, 0f, 64f);

            Undo.RecordObject(mesh, "Generated Secondary UV");
            Unwrapping.GenerateSecondaryUVSet(mesh, unwrapParam);
            EditorUtility.SetDirty(mesh);

            Debug.Log("Generated Secondary UV.");

            SceneView.RepaintAll();
        }


        public void UpdateFoldoutVisibilities()
        {
            UpdateDisplayStyle();

            _container.Q<Button>("OptimizeMesh").style.display =
                _editorSettings.ShowOptimizeButton ? DisplayStyle.Flex : DisplayStyle.None;
            _container.Q<Button>("RecalculateNormals").style.display = _editorSettings.ShowRecalculateNormalsButton
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _container.Q<Button>("RecalculateTangents").style.display = _editorSettings.ShowRecalculateTangentsButton
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _container.Q<Button>("FlipNormals").style.display =
                _editorSettings.ShowFlipNormalsButton ? DisplayStyle.Flex : DisplayStyle.None;
            _container.Q<GroupBox>("GenerateSecondaryUVsetGroupBox").style.display =
                _editorSettings.ShowGenerateSecondaryUVButton ? DisplayStyle.Flex : DisplayStyle.None;
            _container.Q<Button>("exportMesh").style.display =
                _editorSettings.ShowSaveMeshAsButton ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax) =>
            (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;


        static bool MeshIsAnAsset(Mesh newMesh) => AssetDatabase.Contains(newMesh);

        static string OptimizeMeshTooltip() =>
            "Optimizes mesh data to improve rendering performance. You should only use this function on meshes generated procedurally in code. For regular mesh assets, it is automatically called by the import pipeline when Optimize Mesh is enabled in the mesh importer settings."
            + "\n\nThis function reorders the geometry and vertices of the mesh internally to improve vertex cache utilization on the graphics hardware, thereby enhancing rendering performance."
            + "\n\nNote that this operation can take several seconds or more for complex meshes, and it should only be used when the ordering of geometry and vertices is not significant, as both will be changed.";
    }
}