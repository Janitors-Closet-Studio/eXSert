using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    public class DebugGizmoManager
    {
        #region Variable Declarations

        readonly BetterMeshSettings _editorSettings;

        readonly GroupBox _container;

        bool _showNormals = false;
        float _normalLength = 0.1f;
        float _normalWidth = 5;
        Color _normalColor = Color.blue;

        bool _showTangents = false;
        float _tangentLength = 0.1f;
        float _tangentWidth = 5;
        Color _tangentColor = Color.red;

        bool _showUV;
        Color _uvSeamColor = Color.green;
        float _uvWidth = 5;

        GroupBox _gizmoTimeWarningBox;

        FloatField _normalsWidthField;
        FloatField _tangentWidthField;
        FloatField _uvWidthField;

        Label _lastDrawnGizmosWith;
        Label _tooMuchHandleWarningLabel;
        Label _gizmoIsOnWarningLabel;

        List<Transform> _transforms = new();
        List<Mesh> _meshes = new();

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        List<Mesh> _bakedMeshes = new();

        #endregion Variable Declarations


        /// <summary>
        /// Constructor
        /// </summary>
        public DebugGizmoManager(BetterMeshSettings editorSettings, VisualElement root)
        {
            _editorSettings = editorSettings;

            _container = root.Q<GroupBox>("MeshDebugFoldout");
            CustomFoldout.SetupFoldout(_container);


            DrawGizmoSettings(_container);

            UpdateDisplayStyle();
        }

        public void Cleanup()
        {
            ResetMaterials();

            CleanUpBakedMeshes();
        }

        void CleanUpBakedMeshes()
        {
            if (Application.isPlaying)
            {
                foreach (Mesh t in _bakedMeshes)
                {
                    Object.Destroy(t);
                }
            }
            else
            {
                foreach (Mesh t in _bakedMeshes)
                {
                    Object.DestroyImmediate(t);
                }
            }

            _bakedMeshes.Clear();
        }


        /// <summary>
        /// Turns on and off the foldout
        /// </summary>
        public void UpdateDisplayStyle()
        {
            _container.style.display = _editorSettings.ShowDebugGizmoFoldout ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void HideDebugGizmo()
        {
            _container.style.display = DisplayStyle.None;
        }

        void DrawGizmoSettings(VisualElement container)
        {
            _gizmoIsOnWarningLabel = container.Q<Label>("GizmoIsOnWarningLabel");

            NormalsGizmoSettings(container);

            TangentGizmoSettings(container);

            UVGizmoSettings(container);

            CheckeredUVSettings(container);

            Toggle useAntiAliasedGizmosField = container.Q<Toggle>("UseAntiAliasedGizmosField");
            useAntiAliasedGizmosField.value = _editorSettings.useAntiAliasedGizmo;
            useAntiAliasedGizmosField.RegisterValueChangedCallback(e =>
            {
                _editorSettings.useAntiAliasedGizmo = e.newValue;
                _editorSettings.Save();

                SwitchOnOffAAAGizmosFields();
            });

            SwitchOnOffAAAGizmosFields();

            IntegerField maximumGizmoDrawTimeField = container.Q<IntegerField>("MaximumGizmoDrawTimeField");
            maximumGizmoDrawTimeField.value = _editorSettings.maximumGizmoDrawTime;
            maximumGizmoDrawTimeField.RegisterValueChangedCallback(e =>
            {
                // ReSharper disable once MergeIntoPattern
                if (e.newValue < 10 && e.newValue > 10000)
                    return;

                _editorSettings.maximumGizmoDrawTime = e.newValue;
                _editorSettings.Save();
            });

            IntegerField cachedDataUpdateRate = container.Q<IntegerField>("CachedDataUpdateRate");
            cachedDataUpdateRate.value = _editorSettings.updateCacheEveryDashSeconds;
            cachedDataUpdateRate.RegisterValueChangedCallback(e =>
            {
                if (e.newValue < 0)
                    return;

                _editorSettings.updateCacheEveryDashSeconds = e.newValue;
                _editorSettings.Save();
            });

            _gizmoTimeWarningBox = container.Q<GroupBox>("GizmoTimeWarningBox");
            _lastDrawnGizmosWith = container.Q<Label>("LastDrawnGizmosWith");
            _tooMuchHandleWarningLabel = container.Q<Label>("GizmoWarningLabel");
        }

        public void UpdateTargets(List<Mesh> meshes, List<Transform> transforms)
        {
            _meshes ??= new();
            _meshes.Clear();
            _transforms ??= new();
            _transforms.Clear();

            foreach (Mesh mesh in meshes.Where(mesh => mesh != null))
            {
                _meshes.Add(mesh);
            }

            foreach (Transform transform in transforms.Where(transform => transform != null))
            {
                _transforms.Add(transform);
            }

            UpdateCachedData();
        }


        void SwitchOnOffAAAGizmosFields()
        {
            if (_editorSettings.useAntiAliasedGizmo)
            {
                _normalsWidthField.SetEnabled(true);
                _tangentWidthField.SetEnabled(true);
                _uvWidthField.SetEnabled(true);
            }
            else
            {
                _normalsWidthField.SetEnabled(false);
                _tangentWidthField.SetEnabled(false);
                _uvWidthField.SetEnabled(false);
            }
        }

        void NormalsGizmoSettings(VisualElement container)
        {
            Toggle showNormalsField = container.Q<Toggle>("showNormals");
            FloatField normalsLengthField = container.Q<FloatField>("normalLength");
            _normalsWidthField = container.Q<FloatField>("normalWidth");
            ColorField normalsColorField = container.Q<ColorField>("normalColor");

            if (!showNormalsField.value)
                HideNormalsGizmoSettings(normalsLengthField, _normalsWidthField, normalsColorField);

            showNormalsField.RegisterValueChangedCallback(ev =>
            {
                _showNormals = ev.newValue;

                if (ev.newValue)
                {
                    normalsLengthField.style.display = DisplayStyle.Flex;
                    _normalsWidthField.style.display = DisplayStyle.Flex;
                    normalsColorField.style.display = DisplayStyle.Flex;
                }
                else
                {
                    HideNormalsGizmoSettings(normalsLengthField, _normalsWidthField, normalsColorField);
                }

                GizmoToggled();
                SceneView.RepaintAll();
            });
            normalsLengthField.RegisterValueChangedCallback(ev =>
            {
                _normalLength = ev.newValue;
                SceneView.RepaintAll();
            });
            _normalsWidthField.RegisterValueChangedCallback(ev =>
            {
                _normalWidth = ev.newValue;
                SceneView.RepaintAll();
            });
            normalsColorField.RegisterValueChangedCallback(ev =>
            {
                _normalColor = ev.newValue;
                SceneView.RepaintAll();
            });

            GizmoToggled();
        }

        void GizmoToggled()
        {
            if (_showNormals || _showTangents || _showUV)
                _gizmoIsOnWarningLabel.style.display = DisplayStyle.Flex;
            else
                _gizmoIsOnWarningLabel.style.display = DisplayStyle.None;
        }

        static void HideNormalsGizmoSettings(FloatField normalsLengthField, FloatField normalWidthField,
            ColorField normalsColorField)
        {
            normalsLengthField.style.display = DisplayStyle.None;
            normalWidthField.style.display = DisplayStyle.None;
            normalsColorField.style.display = DisplayStyle.None;
        }

        void CheckeredUVSettings(VisualElement myContainer)
        {
            Button setCheckeredUV = myContainer.Q<Button>("setCheckeredUV");
            setCheckeredUV.clickable = null;
            setCheckeredUV.clicked += () =>
            {
                if (_editorSettings.originalMaterials.Count > 0)
                    ResetMaterials();
                else
                    AssignCheckerMaterials();
            };

            Toggle setCheckerField = myContainer.Q<Toggle>("setChecker");
            setCheckerField.RegisterValueChangedCallback(ev =>
            {
                if (ev.newValue)
                {
                    AssignCheckerMaterials();
                }
                else
                {
                    ResetMaterials();
                }
            });
        }

        bool _justAppliedMaterialDoNotReset = false;

        void AssignCheckerMaterials()
        {
            int width = _container.Q<IntegerField>("UVWidth").value;
            int height = _container.Q<IntegerField>("UVHeight").value;
            int cellSize = _container.Q<IntegerField>("UVCellSize").value;

            Undo.SetCurrentGroupName("Set Checkered Materials");
            int group = Undo.GetCurrentGroup();

            for (int i = 0; i < _transforms.Count; i++)
            {
                AssignCheckerMaterial(i, width, height, cellSize);
            }

            Undo.CollapseUndoOperations(group);

            // todo Find a cleaner way to work around this
            // If not in multi select, when only one target is selected, the inspector resets. This avoids the checker material from going away.
            if (_transforms.Count == 1)
                _justAppliedMaterialDoNotReset = true; //If this is true
        }

        void AssignCheckerMaterial(int index, int width, int height, int cellSize)
        {
            Debug.Log("Assigning checker material.");
            Renderer renderer = _transforms[index].GetComponent<Renderer>();

            if (renderer == null)
            {
                return;
            }

            OriginalMaterial original = new(renderer.sharedMaterials);
            Material[] tempMaterials = renderer.sharedMaterials;

            for (int i = 0; i < tempMaterials.Length; i++)
            {
                Material originalMaterial = tempMaterials[i];

                if (originalMaterial == null) continue;

                Material checkerMaterial = new(originalMaterial)
                {
                    name = "Checkered Material",
                    mainTexture = CreateCheckeredTexture(width, height, cellSize)
                };

                tempMaterials[i] = checkerMaterial;
            }

            _editorSettings.originalMaterials.Add(original);

            Undo.RecordObject(renderer, "Set Checkered Materials");
            renderer.sharedMaterials = tempMaterials;
        }

        void ResetMaterials()
        {
            if (_justAppliedMaterialDoNotReset)
            {
                _justAppliedMaterialDoNotReset = false;
                return;
            }

            for (int i = 0; _transforms.Count > i; i++)
            {
                if (_transforms[i] == null) continue;

                if (!_transforms[i].GetComponent<Renderer>()) continue;

                if (_editorSettings.originalMaterials.Count <= i) continue;

                _transforms[i].GetComponent<Renderer>().sharedMaterials =
                    _editorSettings.originalMaterials[i].materials;
            }

            _editorSettings.originalMaterials.Clear();
        }

        static Texture2D CreateCheckeredTexture(int width, int height, int checkSize)
        {
            Texture2D texture = new(width, height);
            Color color1 = Color.black;
            Color color2 = Color.white;

            // Loop over the width and height.
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    // Determine which color to use based on the current x and y indices.
                    bool checkX = x / checkSize % 2 == 0;
                    bool checkY = y / checkSize % 2 == 0;
                    Color color = checkX == checkY ? color1 : color2;

                    // Set the pixel color.
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();

            return texture;
        }

        void UVGizmoSettings(VisualElement container)
        {
            Toggle showUVField = container.Q<Toggle>("showUV");
            _uvWidthField = container.Q<FloatField>("uvWidth");
            ColorField uvColorField = container.Q<ColorField>("uvColor");

            if (!showUVField.value) HideUVGizmoSettings(_uvWidthField, uvColorField);

            showUVField.RegisterValueChangedCallback(ev =>
            {
                _showUV = ev.newValue;

                if (ev.newValue)
                {
                    _uvWidthField.style.display = DisplayStyle.Flex;
                    uvColorField.style.display = DisplayStyle.Flex;
                }
                else
                {
                    HideUVGizmoSettings(_uvWidthField, uvColorField);
                }

                GizmoToggled();
                SceneView.RepaintAll();
            });
            _uvWidthField.RegisterValueChangedCallback(ev =>
            {
                _uvWidth = ev.newValue;
                SceneView.RepaintAll();
            });
            uvColorField.RegisterValueChangedCallback(ev =>
            {
                _uvSeamColor = ev.newValue;
                SceneView.RepaintAll();
            });
        }

        static void HideUVGizmoSettings(FloatField uvWidthField, ColorField uvColorField)
        {
            uvWidthField.style.display = DisplayStyle.None;
            uvColorField.style.display = DisplayStyle.None;
        }

        void TangentGizmoSettings(VisualElement container)
        {
            Toggle showTangentsField = container.Q<Toggle>("showTangents");
            FloatField tangentLengthField = container.Q<FloatField>("tangentLength");
            _tangentWidthField = container.Q<FloatField>("tangentWidth");
            ColorField tangentColorField = container.Q<ColorField>("tangentColor");

            if (!showTangentsField.value)
                HideTangentGizmoSettings(tangentLengthField, _tangentWidthField, tangentColorField);

            showTangentsField.RegisterValueChangedCallback(ev =>
            {
                _showTangents = ev.newValue;

                if (ev.newValue)
                {
                    tangentLengthField.style.display = DisplayStyle.Flex;
                    _tangentWidthField.style.display = DisplayStyle.Flex;
                    tangentColorField.style.display = DisplayStyle.Flex;
                }
                else
                {
                    HideTangentGizmoSettings(tangentLengthField, _tangentWidthField, tangentColorField);
                }

                GizmoToggled();
                SceneView.RepaintAll();
            });
            tangentLengthField.RegisterValueChangedCallback(ev =>
            {
                _tangentLength = ev.newValue;
                SceneView.RepaintAll();
            });
            _tangentWidthField.RegisterValueChangedCallback(ev =>
            {
                _tangentWidth = ev.newValue;
                SceneView.RepaintAll();
            });
            tangentColorField.RegisterValueChangedCallback(ev =>
            {
                _tangentColor = ev.newValue;
                SceneView.RepaintAll();
            });
        }

        static void HideTangentGizmoSettings(FloatField tangentLengthField, FloatField tangentWidthField,
            ColorField tangentColorField)
        {
            tangentLengthField.style.display = DisplayStyle.None;
            tangentWidthField.style.display = DisplayStyle.None;
            tangentColorField.style.display = DisplayStyle.None;
        }

        Stopwatch _stopwatch;
        bool _useAntiAliasedHandles;
        int _drawnGizmo;
        int _maximumGizmoTime;
        bool _wasAbleToDrawEverything = true;

        public void DrawGizmo(SkinnedMeshRenderer[] skinnedMeshRenderers = null)
        {
            if (_meshes == null) return;
            if (_meshes.Count == 0) return;

            if (!_editorSettings) return;

            if (!_editorSettings.ShowDebugGizmoFoldout) return;

            if (!_showNormals && !_showTangents && !_showUV) return;

            if (_stopwatch == null) _stopwatch = new();
            else _stopwatch.Reset();

            _stopwatch.Start();

            _useAntiAliasedHandles = _editorSettings.useAntiAliasedGizmo;
            _maximumGizmoTime = _editorSettings.maximumGizmoDrawTime;
            _drawnGizmo = 0;

            _wasAbleToDrawEverything = true;

            if ((skinnedMeshRenderers != null && (_bakedMeshes == null || _bakedMeshes.Count == 0)) ||
                Time.realtimeSinceStartup - _timeSinceLastCachedDataUpdate >
                _editorSettings.updateCacheEveryDashSeconds) UpdateCachedData(skinnedMeshRenderers);

            if (skinnedMeshRenderers == null)
            {
                for (int i = 0; i < _meshes.Count; i++)
                {
                    if (_stopwatch.ElapsedMilliseconds > _maximumGizmoTime)
                    {
                        _stopwatch.Stop();
                        break;
                    }

                    if (i >= _transforms.Count || i >= _cachedMeshData.Count) return;

                    DrawGizmoForMesh(_meshes[i], _transforms[i], _cachedMeshData[i]);
                }
            }
            else
            {
                if (_bakedMeshes != null)
                    for (int i = 0; i < _bakedMeshes.Count; i++)
                    {
                        if (_stopwatch.ElapsedMilliseconds > _maximumGizmoTime)
                        {
                            _stopwatch.Stop();
                            //MaximumGizmoDrawingTimeReachedForVertexAndTriangles(vertices.Length - (i + 1));
                            break;
                        }

                        if (i >= _transforms.Count || i >= _cachedMeshData.Count) return;

                        DrawGizmoForMesh(_bakedMeshes[i], skinnedMeshRenderers[i].transform, _cachedMeshData[i]);
                    }
            }


            _stopwatch.Stop();
            GizmosDrawingDone(_drawnGizmo, _stopwatch.ElapsedMilliseconds);
        }

        const float UVSeamThreshold = 0.001f;
        const int MaxUVSeamForAaaHandle = 30000;

        void DrawGizmoForMesh(Mesh mesh, Transform transform, CachedMeshData cachedMeshData)
        {
            if (!mesh) return;
            Vector3[] vertices = mesh.vertices;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            if (_showUV)
            {
                Handles.color = _uvSeamColor;

                if (_useAntiAliasedHandles && cachedMeshData.LinePoints.Length < MaxUVSeamForAaaHandle)
                    Handles.DrawAAPolyLine(_uvWidth, cachedMeshData.LinePoints);
                else
                    Handles.DrawLines(cachedMeshData.LinePoints);

                _drawnGizmo += cachedMeshData.LinePoints.Length;
            }

            if (!_showNormals && !_showTangents) return;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;

            Matrix4x4 normalMatrix = transform.localToWorldMatrix;

            bool drawNormals = _showNormals && normals.Length == vertices.Length;
            bool drawTangents = _showTangents && tangents.Length == vertices.Length;

            for (int i = 0; i < vertices.Length; i++)
            {
                //Vector3 worldVertex = transform.TransformPoint(vertices[i]);
                Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertices[i]);

                if (drawNormals)
                {
                    //Vector3 worldNormal = transform.TransformDirection(normals[i]);
                    Vector3 worldNormal = normalMatrix.MultiplyVector(normals[i]);
                    Handles.color = _normalColor;
                    if (_useAntiAliasedHandles)
                        Handles.DrawAAPolyLine(_normalWidth, worldVertex,
                            worldVertex + worldNormal * _normalLength);
                    else
                        Handles.DrawLine(worldVertex, worldVertex + worldNormal * _normalLength);
                }

                if (drawTangents)
                {
                    //Vector3 worldTangent = transform.TransformDirection(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z));
                    Vector3 worldTangent = normalMatrix.MultiplyVector(tangents[i]);
                    Handles.color = _tangentColor;
                    if (_useAntiAliasedHandles)
                        Handles.DrawAAPolyLine(_tangentWidth, worldVertex,
                            worldVertex + worldTangent * _tangentLength);
                    else
                        Handles.DrawLine(worldVertex, worldVertex + worldTangent * _tangentLength);
                }

                if (_stopwatch.ElapsedMilliseconds <= _maximumGizmoTime) continue;
                _stopwatch.Stop();

                if (_showNormals)
                    _drawnGizmo += i + 1;
                if (_showTangents)
                    _drawnGizmo += i + 1;

                MaximumGizmoDrawingTimeReachedForVertexAndTriangles(vertices.Length - (i + 1));
                return;
            }

            if (_showNormals)
                _drawnGizmo += vertices.Length;
            if (_showTangents)
                _drawnGizmo += tangents.Length;
        }

        float _timeSinceLastCachedDataUpdate;

        void UpdateCachedData(SkinnedMeshRenderer[] skinnedMeshRenderers = null)
        {
            if (_transforms.Count != _meshes.Count) return;

            _cachedMeshData ??= new();
            _cachedMeshData.Clear();

            if (skinnedMeshRenderers == null)
            {
                for (int i = 0; i < _meshes.Count; i++)
                {
                    if (!_meshes[i] || !_transforms[i]) continue;

                    UpdateCachedData(_meshes[i], _transforms[i]);
                }
            }
            else
            {
                CleanUpBakedMeshes();

                foreach (SkinnedMeshRenderer t in skinnedMeshRenderers)
                {
                    if (!t.sharedMesh) continue;

                    Mesh bakedMesh = new();
                    t.BakeMesh(bakedMesh);
                    _bakedMeshes.Add(bakedMesh);

                    UpdateCachedData(bakedMesh, t.transform);
                }
            }


            _timeSinceLastCachedDataUpdate = Time.realtimeSinceStartup;
        }

        void UpdateCachedData(Mesh mesh, Transform transform)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;

            Vector2[] uvs = mesh.uv;
            if (uvs.Length == 0) return;

            //float threshold = 0.5f * 0.5f; // Compare squared distances to avoid sqrt calculations
            const float threshold = UVSeamThreshold * UVSeamThreshold; // Compare squared distances to avoid sqrt calculations

            int triangleCount = triangles.Length;

            Handles.color = _uvSeamColor;

            List<Triangle> trianglesStruct = new();
            for (int i = 0; i < triangleCount; i += 3)
            {
                int indexA = triangles[i];
                int indexB = triangles[i + 1];
                int indexC = triangles[i + 2];

                Vector2 uvA = uvs[indexA];
                Vector2 uvB = uvs[indexB];
                Vector2 uvC = uvs[indexC];

                if ((uvA - uvB).sqrMagnitude > threshold || (uvB - uvC).sqrMagnitude > threshold ||
                    (uvC - uvA).sqrMagnitude > threshold)
                {
                    trianglesStruct.Add(new(localToWorld.MultiplyPoint3x4(vertices[indexA]),
                        localToWorld.MultiplyPoint3x4(vertices[indexB]),
                        localToWorld.MultiplyPoint3x4(vertices[indexC])));
                }
            }

            List<Vector3> linePoints = new();
            foreach (Triangle tri in trianglesStruct)
            {
                linePoints.Add(tri.WorldVertexA);
                linePoints.Add(tri.WorldVertexB);

                linePoints.Add(tri.WorldVertexB);
                linePoints.Add(tri.WorldVertexC);

                linePoints.Add(tri.WorldVertexC);
                linePoints.Add(tri.WorldVertexA);
            }

            _cachedMeshData.Add(new(linePoints));
        }

        List<CachedMeshData> _cachedMeshData = new();

        struct CachedMeshData
        {
            public readonly Vector3[] LinePoints;

            public CachedMeshData(List<Vector3> linePointsList) : this()
            {
                LinePoints = linePointsList.ToArray();
            }
        }

        struct Triangle
        {
            public Triangle(Vector3 worldVertexA, Vector3 worldVertexB, Vector3 worldVertexC) : this()
            {
                WorldVertexA = worldVertexA;
                WorldVertexB = worldVertexB;
                WorldVertexC = worldVertexC;
            }

            public Vector3 WorldVertexA { get; }
            public Vector3 WorldVertexB { get; }
            public Vector3 WorldVertexC { get; }
        }

        /// <summary>
        /// This is called at the end after everything is drawn.
        /// </summary>
        /// <param name="gizmosDrawn"></param>
        /// <param name="time"></param>
        void GizmosDrawingDone(int gizmosDrawn, long time)
        {
            if (_lastDrawnGizmosWith == null)
                return;

            if (gizmosDrawn > 0)
                _lastDrawnGizmosWith.text =
                    "Drew <b>" + gizmosDrawn + "</b> handles and it took <b>" + time + "</b>ms.";
            else
                _lastDrawnGizmosWith.text = "";

            if (_wasAbleToDrawEverything)
                _gizmoTimeWarningBox.style.display = DisplayStyle.None;
        }

        void MaximumGizmoDrawingTimeReachedForVertexAndTriangles(int gizmoNotDrawnFor)
        {
            _wasAbleToDrawEverything = false;

            if (_tooMuchHandleWarningLabel == null)
                return;

            _gizmoTimeWarningBox.style.display = DisplayStyle.Flex;

            _tooMuchHandleWarningLabel.text = "Didn't draw <b>" + gizmoNotDrawnFor + "</b> handles for ";
            switch (_showNormals)
            {
                case true when _showTangents:
                    _tooMuchHandleWarningLabel.text += "normals and tangents.";
                    break;
                case true:
                    _tooMuchHandleWarningLabel.text += "normals.";
                    break;
                default:
                {
                    if (_showTangents)
                        _tooMuchHandleWarningLabel.text += "tangents.";
                    break;
                }
            }

            if (_editorSettings.useAntiAliasedGizmo)
                _tooMuchHandleWarningLabel.text += "\n\nTurning off anti aliased gizmos will help the performance";
        }
    }
}