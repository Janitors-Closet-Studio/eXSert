using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    /// <summary>
    /// The editors pass mesh to this, and this script handles creating and cleaning up previews.
    /// </summary>
    public class BetterMeshPreviewManager
    {
        // URL to documentation or help page related to runtime memory usage
        const string LearnMoreAboutRuntimeMemoryUsageLink =
            "https://ferdowsur.gitbook.io/better-mesh/full-feature-list/runtime-memory-size";

        // Stores a list of mesh preview data used for drawing previews in the inspector. This is used to clean up memory when the inspector is deselected.
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        List<MeshPreview> _meshPreviews = new();

        // UXML template used for generating each mesh preview UI
        readonly VisualTreeAsset _previewTemplate;

        // Path to the UXML file for mesh previews
        const string PreviewTemplateLocation = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Mesh Filter/Scripts/Editor/Templates/MeshPreview.uxml";
        const string PreviewTemplateGuid = "7358ad733b4a05a4c88dbecb820153e8";

        // Custom GUIStyle used when drawing elements with colored backgrounds
        readonly GUIStyle _style;

        // Total height allocated for the mesh previews
        float _previewHeight;

        // Root container of the UI;
        readonly VisualElement _container;

        // Contains the IMGUI preview boxes
        readonly VisualElement _previewsGroupBox;

        readonly GroupBox _allSelectedMeshCombinedDetails;
        readonly Label _maxPreviewCount;

        /// <summary>
        /// Container for the IMGUI preview section. Its height can be resized using the drag handle.
        /// </summary>
        VisualElement _imguiPreviewContainer;

        // Drag handle UI Element for resizing the IMGUI preview container
        VisualElement _dragHandle;

        readonly GroupBox _informationFoldout;
        readonly GroupBox _assetLocationInInformationFoldoutGroupBox;

        // Stores reference to the current user settings for Better Mesh
        readonly BetterMeshSettings _editorSettings;

        /// <summary>
        /// Frees up allocated memory and UI elements when the editor window is closed.
        /// Must be called during tear-down to prevent memory leaks.
        /// </summary>
        public void CleanUp()
        {
            // Clear UI hierarchy if initialized
            _previewsGroupBox?.Clear();

            // Dispose of all mesh preview elements  
            for (int index = 0; index < _meshPreviews.Count; index++)
            {
                if (_meshPreviews[index] == null) continue;

                _meshPreviews[index].Dispose();
                DomainReloadCleanup.Unregister(_meshPreviews[index]);
                _meshPreviews[index] = null;
            }

            // Clear the list to release references
            _meshPreviews.Clear();
        }

        public BetterMeshPreviewManager(BetterMeshSettings editorSettings, VisualElement root)
        {
            _editorSettings = editorSettings;

            _container = root.Q<TemplateContainer>("MeshPreviewContainers");
            _previewsGroupBox = _container.Q<ScrollView>("PreviewsGroupBox");

            _previewTemplate = Utility.GetVisualTreeAsset(PreviewTemplateLocation, PreviewTemplateGuid);

            // if (_previewTemplate == null)
            // {
            //     Object[] objects = Resources.FindObjectsOfTypeAll(typeof(VisualTreeAsset));
            //     foreach (Object obj in objects)
            //     {
            //         VisualTreeAsset v = (VisualTreeAsset)obj;
            //         if (v.name != "MeshPreview") continue;
            //         _previewTemplate = v;
            //         break;
            //     }
            // }

            // Initialize preview style with background color from settings
            _style = new()
            {
                normal = { background = BackgroundTexture(BetterMeshSettings.instance.PreviewBackgroundColor) }
            };
            _previewHeight = GetMeshPreviewHeight();

            // Setup background color field UI and hook into color change callback
            ColorField previewColor = _previewsGroupBox.parent.Q<ColorField>("PreviewColorField");
            previewColor.value = editorSettings.PreviewBackgroundColor;
            previewColor.RegisterValueChangedCallback(ev =>
            {
                editorSettings.PreviewBackgroundColor = ev.newValue;
                _style.normal.background = BackgroundTexture(ev.newValue);
            });

            _allSelectedMeshCombinedDetails = _container.Q<GroupBox>("AllSelectedMeshCombinedDetails");
            _maxPreviewCount = _allSelectedMeshCombinedDetails.Q<Label>("MaxPreviewCount");

            _informationFoldout = root.Q<GroupBox>("Information");
            CustomFoldout.SetupFoldout(_informationFoldout);

            _assetLocationInInformationFoldoutGroupBox =
                _informationFoldout.Q<GroupBox>("AssetLocationInFoldoutGroupBox");
        }

        void UpdateInformationFoldout(List<Mesh> meshes)
        {
            if (!_editorSettings.ShowInformationFoldout)
            {
                _informationFoldout.style.display = DisplayStyle.None;
                return;
            }

            _informationFoldout.style.display = DisplayStyle.Flex;

            UpdateMeshDataGroup(_informationFoldout.Q<TemplateContainer>("MeshDataGroup"), meshes, true);

            _assetLocationInInformationFoldoutGroupBox.style.display =
                _editorSettings.ShowAssetLocationInFoldout ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void HideInformationFoldout()
        {
            _informationFoldout.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Sets up the preview manager with settings and UI elements, but does not generate the actual mesh previews.
        /// </summary>
        public void SetupPreviewManager(List<Mesh> meshes, int targetCount)
        {
            Label totalSelectionCount = _container.Q<Label>("TotalSelectionCount");
            if (targetCount > 0)
            {
                totalSelectionCount.style.display = DisplayStyle.Flex;
                totalSelectionCount.text = "" + targetCount + " targets selected";
            }
            else
            {
                totalSelectionCount.style.display = DisplayStyle.None;
            }

            SetupHeightResizeDragHandles(meshes);
            CreatePreviews(meshes);
        }

        void SetupHeightResizeDragHandles(List<Mesh> meshes)
        {
            _dragHandle = _previewsGroupBox.parent.Q<VisualElement>("DragHandle");

            if (meshes.Count > 1)
            {
                // Hide Drag Handle for multi-mesh view (fixed size layout)
                _dragHandle.style.display = DisplayStyle.None;
            }
            else
            {
                // Enable Drag Handle for single mesh view
                _dragHandle.style.display = DisplayStyle.Flex;
                _dragHandle.RegisterCallback<MouseDownEvent>(OnMouseDown);
                _dragHandle.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                _dragHandle.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }
        }

        public void CreatePreviews(List<Mesh> meshes)
        {
            CleanUp();

            if (meshes.Count > 0 && _editorSettings.ShowMeshPreview)
            {
                _container.style.display = DisplayStyle.Flex;
            }
            else
            {
                _container.style.display = DisplayStyle.None;

                if (_editorSettings.ShowInformationFoldout)
                    UpdateMeshDataGroup(_informationFoldout.Q<TemplateContainer>("MeshDataGroup"), meshes,
                        _editorSettings.ShowMeshDetailsUnderPreview);

                return;
            }


            if (meshes.Count > 1 && _editorSettings.ShowMeshDetailsUnderPreview)
            {
                _allSelectedMeshCombinedDetails.style.display = DisplayStyle.Flex;
                _allSelectedMeshCombinedDetails.Q<Label>("TotalMeshCount").text = " with " + meshes.Count + " meshes.";
                UpdateMeshDataGroup(_allSelectedMeshCombinedDetails.Q<TemplateContainer>("MeshDataGroup"), meshes,
                    _editorSettings.ShowMeshDetailsUnderPreview);
            }
            else
            {
                _allSelectedMeshCombinedDetails.style.display = DisplayStyle.None;
            }

            int previewAmount = meshes.Count;

            if (_editorSettings.MaxPreviewCount < previewAmount)
            {
                previewAmount = _editorSettings.MaxPreviewCount;
                _maxPreviewCount.text = "Max preview count " + _editorSettings.MaxPreviewCount +
                                        " reached. Change amount from settings.";
                _maxPreviewCount.style.display = DisplayStyle.Flex;
            }
            else
            {
                _maxPreviewCount.style.display = DisplayStyle.None;
            }

            for (int i = 0; i < previewAmount; i++)
            {
                CreatePreviewForMesh(meshes[i]);
            }


            UpdateInformationFoldout(meshes);
        }


        void CreatePreviewForMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            // Clone template and add to the UI
            VisualElement previewBase = new();
            _previewTemplate.CloneTree(previewBase);
            previewBase.style.flexGrow = 1;
            _previewsGroupBox.Add(previewBase);

            // Cache required UI elements
            IMGUIContainer previewContainer = previewBase.Q<IMGUIContainer>("PreviewContainer");
            IMGUIContainer previewSettingsContainer = previewBase.Q<IMGUIContainer>("PreviewSettingsContainer");

            previewContainer.style.height = _previewHeight;
            // Cached to enable resize by dragging the handle. Is not useful when multiple mesh is selected
            _imguiPreviewContainer = previewContainer;

            // Initialize and store the preview logic
            MeshPreview meshPreview = new(mesh);

            _meshPreviews.Add(meshPreview);
            DomainReloadCleanup.Register(meshPreview);

            // If failed to create preview, return.
            if (meshPreview == null)
                return;

            previewSettingsContainer.onGUIHandler += () =>
            {
                //GUI.contentColor = Color.white;
                //GUI.color = Color.white;

                //GUILayout.BeginHorizontal("Box");
                GUILayout.BeginHorizontal();
                meshPreview.OnPreviewSettings();
                GUILayout.EndHorizontal();
            };

            previewContainer.onGUIHandler += () =>
            {
                switch (previewContainer.contentRect.height)
                {
                    case <= 0:
                        previewContainer.style.height = 50;
                        break;
                    //Should be unnecessary. But still fixes a bug with height and width being negative.
                    case > 0 when
                        previewContainer.contentRect.width >
                        0:
                        meshPreview.OnPreviewGUI(previewContainer.contentRect, _style);
                        break;
                }
            };

            UpdateMeshDataGroup(previewBase.Q<GroupBox>("MeshDataGroup"), mesh);
        }

        static Texture2D BackgroundTexture(Color color)
        {
            Texture2D newTexture = new(1, 1);
            newTexture.SetPixel(0, 0, color);
            newTexture.Apply();
            return newTexture;
        }

        /// <summary>
        /// This is for the total information counter.
        /// </summary>
        void UpdateMeshDataGroup(TemplateContainer meshDataGroup, List<Mesh> meshes, bool show)
        {
            if (!show)
            {
                meshDataGroup.style.display = DisplayStyle.None;
                return;
            }

            GroupBox verticesGroup = meshDataGroup.Q<GroupBox>("VerticesGroup");
            if (!_editorSettings.ShowVertexInformation) verticesGroup.style.display = DisplayStyle.None;
            else
            {
                verticesGroup.style.display = DisplayStyle.Flex;

                int counter = meshes.Where(mesh => mesh != null).Sum(mesh => mesh.vertexCount);

                verticesGroup.Q<Label>("Value").text = counter.ToString(CultureInfo.InvariantCulture);

                GroupBox submeshGroup = meshDataGroup.Q<GroupBox>("SubmeshGroup");
                if (meshes.Count == 1)
                {
                    int[] subMeshVertexCounts = meshes[0].SubMeshVertexCount();
                    if (subMeshVertexCounts is { Length: > 1 })
                    {
                        submeshGroup.style.display = DisplayStyle.Flex;
                        submeshGroup.Q<Label>("SubmeshValue").text =
                            subMeshVertexCounts.Length.ToString(CultureInfo.InvariantCulture);
                        Label submeshVertices = submeshGroup.Q<Label>("SubmeshVertices");
                        submeshVertices.text = "(";
                        for (int i = 0; i < subMeshVertexCounts.Length; i++)
                        {
                            submeshVertices.text += subMeshVertexCounts[i];

                            if (i + 1 != subMeshVertexCounts.Length)
                                submeshVertices.text += ", ";
                        }

                        submeshVertices.text += ")";
                    }
                    else
                    {
                        submeshGroup.style.display = DisplayStyle.None;
                    }
                }
                else
                {
                    submeshGroup.style.display = DisplayStyle.None;
                }
            }

            GroupBox trianglesGroup = meshDataGroup.Q<GroupBox>("TrianglesGroup");
            if (!_editorSettings.ShowTriangleInformation) trianglesGroup.style.display = DisplayStyle.None;
            else
            {
                trianglesGroup.style.display = DisplayStyle.Flex;

                int counter = meshes.Where(mesh => mesh != null).Sum(mesh => mesh.TrianglesCount());
                trianglesGroup.Q<Label>("Value").text = counter.ToString(CultureInfo.InvariantCulture);
            }

            GroupBox edgeGroup = meshDataGroup.Q<GroupBox>("EdgeGroup");
            if (!_editorSettings.ShowEdgeInformation) edgeGroup.style.display = DisplayStyle.None;
            else
            {
                edgeGroup.style.display = DisplayStyle.Flex;

                int counter = meshes.Where(mesh => mesh != null).Sum(mesh => mesh.EdgeCount());
                edgeGroup.Q<Label>("Value").text = counter.ToString(CultureInfo.InvariantCulture);
            }

            GroupBox tangentsGroup = meshDataGroup.Q<GroupBox>("TangentsGroup");
            if (!_editorSettings.ShowTangentInformation) tangentsGroup.style.display = DisplayStyle.None;
            else
            {
                tangentsGroup.style.display = DisplayStyle.Flex;

                int counter = meshes.Where(mesh => mesh != null).Sum(mesh => mesh.tangents.Length);
                tangentsGroup.Q<Label>("Value").text = counter.ToString(CultureInfo.InvariantCulture);
            }

            GroupBox faceGroup = meshDataGroup.Q<GroupBox>("FaceGroup");
            if (!_editorSettings.ShowFaceInformation) faceGroup.style.display = DisplayStyle.None;
            else
            {
                faceGroup.style.display = DisplayStyle.Flex;

                int counter = meshes.Where(mesh => mesh != null).Sum(mesh => mesh.FaceCount());
                faceGroup.Q<Label>("Value").text = counter.ToString(CultureInfo.InvariantCulture);
            }

            GroupBox meshMemoryGroupBox = meshDataGroup.Q<GroupBox>("RuntimeMemoryUsageGroupBox");
            if (_editorSettings.runtimeMemoryUsageUnderPreview)
            {
                meshMemoryGroupBox.style.display = DisplayStyle.Flex;
                long memoryUsageInByte = meshes.Where(mesh => mesh != null).Sum(MemoryUsageInByte);

                meshMemoryGroupBox.Q<Label>("MemoryUsageInFoldout").text = ByteToReadableString(memoryUsageInByte);
                Button label = meshMemoryGroupBox.Q<Button>();
                if (_editorSettings.showRunTimeMemoryUsageLabel)
                {
                    label.style.display = DisplayStyle.Flex;
                    meshMemoryGroupBox.Q<Button>().clicked += () => { Application.OpenURL(LearnMoreAboutRuntimeMemoryUsageLink); };
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                }
            }
            else
                meshMemoryGroupBox.style.display = DisplayStyle.None;
        }

        void UpdateMeshDataGroup(VisualElement meshDataGroup, Mesh mesh)
        {
            if (!_editorSettings.ShowMeshDetailsUnderPreview)
            {
                meshDataGroup.style.display = DisplayStyle.None;
                return;
            }

            GroupBox verticesGroup = meshDataGroup.Q<GroupBox>("VerticesGroup");
            if (!_editorSettings.ShowVertexInformation) verticesGroup.style.display = DisplayStyle.None;
            else
            {
                verticesGroup.style.display = DisplayStyle.Flex;
                verticesGroup.Q<Label>("Value").text = mesh.vertexCount.ToString(CultureInfo.InvariantCulture);

                GroupBox submeshGroup = meshDataGroup.Q<GroupBox>("SubmeshGroup");
                int[] subMeshVertexCounts = mesh.SubMeshVertexCount();
                if (subMeshVertexCounts is { Length: > 1 })
                {
                    submeshGroup.style.display = DisplayStyle.Flex;
                    submeshGroup.Q<Label>("SubmeshValue").text =
                        subMeshVertexCounts.Length.ToString(CultureInfo.InvariantCulture);
                    Label submeshVertices = submeshGroup.Q<Label>("SubmeshVertices");
                    submeshVertices.text = "(";
                    for (int i = 0; i < subMeshVertexCounts.Length; i++)
                    {
                        submeshVertices.text += subMeshVertexCounts[i];
                        if (i + 1 != subMeshVertexCounts.Length)
                            submeshVertices.text += ", ";
                    }

                    submeshVertices.text += ")";
                }
                else
                {
                    submeshGroup.style.display = DisplayStyle.None;
                }
            }

            GroupBox trianglesGroup = meshDataGroup.Q<GroupBox>("TrianglesGroup");
            if (!_editorSettings.ShowTriangleInformation) trianglesGroup.style.display = DisplayStyle.None;
            else
            {
                trianglesGroup.style.display = DisplayStyle.Flex;
                trianglesGroup.Q<Label>("Value").text = mesh.TrianglesCount().ToString(CultureInfo.InvariantCulture);
            }

            GroupBox edgeGroup = meshDataGroup.Q<GroupBox>("EdgeGroup");
            if (!_editorSettings.ShowEdgeInformation) edgeGroup.style.display = DisplayStyle.None;
            else
            {
                edgeGroup.style.display = DisplayStyle.Flex;
                edgeGroup.Q<Label>("Value").text = mesh.EdgeCount().ToString(CultureInfo.InvariantCulture);
            }

            GroupBox tangentsGroup = meshDataGroup.Q<GroupBox>("TangentsGroup");
            if (!_editorSettings.ShowTangentInformation) tangentsGroup.style.display = DisplayStyle.None;
            else
            {
                tangentsGroup.style.display = DisplayStyle.Flex;
                tangentsGroup.Q<Label>("Value").text = mesh.tangents.Length.ToString(CultureInfo.InvariantCulture);
            }

            GroupBox faceGroup = meshDataGroup.Q<GroupBox>("FaceGroup");
            if (!_editorSettings.ShowFaceInformation) faceGroup.style.display = DisplayStyle.None;
            else
            {
                faceGroup.style.display = DisplayStyle.Flex;
                faceGroup.Q<Label>("Value").text = mesh.FaceCount().ToString(CultureInfo.InvariantCulture);
            }

            GroupBox meshMemoryGroupBox = meshDataGroup.Q<GroupBox>("RuntimeMemoryUsageGroupBox");
            if (_editorSettings.runtimeMemoryUsageUnderPreview)
            {
                meshMemoryGroupBox.style.display = DisplayStyle.Flex;
                meshMemoryGroupBox.Q<Label>("MemoryUsageInFoldout").text = GetMemoryUsage(mesh);

                Button label = meshMemoryGroupBox.Q<Button>();

                if (_editorSettings.showRunTimeMemoryUsageLabel)
                {
                    label.style.display = DisplayStyle.Flex;
                    meshMemoryGroupBox.Q<Button>().clicked += () => { Application.OpenURL(LearnMoreAboutRuntimeMemoryUsageLink); };
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                }
            }
            else
                meshMemoryGroupBox.style.display = DisplayStyle.None;
        }

        static float GetMeshPreviewHeight()
        {
            if (BetterMeshSettings.instance.MeshPreviewHeight == 0) return 2;
            return Mathf.Abs(BetterMeshSettings.instance.MeshPreviewHeight);
        }

        static string GetMemoryUsage(Mesh mesh)
        {
            long usageInByte = MemoryUsageInByte(mesh);
            string usage = ByteToReadableString(usageInByte);
            return usage;
        }

        // /// <summary>
        // /// Original - Wrong result
        // /// </summary>
        // /// <param name="usageInByte"></param>
        // /// <returns></returns>
        // static string ByteToReadableString(long usageInByte)
        // {
        //     long megabytes = usageInByte / 1024;
        //     long remainingKilobytes = usageInByte % 1024;
        //
        //     string usage = "";
        //     if (megabytes > 0) usage += megabytes + "MB ";
        //     if (remainingKilobytes > 0) usage += remainingKilobytes + "KB";
        //     return usage;
        // }
        
        // Fix provided by Florin C
        // static string ByteToReadableString(long usageInByte)
        // {
        //     //CustomPatch: fixed MB and KB calculation
        //     const int kOneMBInBytes = 1024 * 1024;
        //     long megabytes = usageInByte / kOneMBInBytes;
        //     long remainingKilobytes = (usageInByte % kOneMBInBytes) / 1024;
        //
        //     string usage = "";
        //     if (megabytes > 0) usage += megabytes + "MB ";
        //     if (remainingKilobytes > 0) usage += remainingKilobytes + "KB";
        //     return usage;
        // }
        
        const long OneKilobyte = 1024;
        const long OneMegabyte = OneKilobyte * 1024;
        const long OneGigabyte = OneMegabyte * 1024;
        // Slightly reorganized/modified fix
        static string ByteToReadableString(long usageInBytes)
        {
            if (usageInBytes < 0) //Never got any error regarding this. But the code editor Rider keeps suggesting me to add it 
                return "0KB";
            
            //Added for just in-case scenarios. Doubt anyone would ever reach this regularly to be needed.
            long gigabytes = usageInBytes / OneGigabyte;
            long remainingAfterGigabyte = usageInBytes % OneGigabyte;
            
            long megabytes = remainingAfterGigabyte / OneMegabyte;
            long remainingAfterMegabyte = remainingAfterGigabyte % OneMegabyte;
            
            long kilobytes = remainingAfterMegabyte / OneKilobyte;
            long bytes = remainingAfterMegabyte % OneKilobyte;
            
            string usage = string.Empty;
            
            if (gigabytes > 0) //No need to add Gigabyte when it is zero
                usage += $"{gigabytes}GB ";
            
            if (megabytes > 0) //No need to add Megabyte when it is zero
                usage += $"{megabytes}MB ";
        
            if (kilobytes > 0) //No need to add Kilobyte when it is zero
                usage += $"{kilobytes}KB ";
        
            if (bytes > 0 && megabytes == 0) // only show bytes if below 1MB
                usage += $"{bytes}B";
        
            return string.IsNullOrWhiteSpace(usage) ? "0B" : usage.TrimEnd();
        }

        static long MemoryUsageInByte(Mesh mesh)
        {
            return mesh == null ? 0 : GetMemoryUsageInByte(mesh);
        }

        static long GetMemoryUsageInByte(Mesh mesh)
        {
            return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh);
        }

        bool _isDragging;
        Vector2 _startMousePosition;
        Vector2 _startSize;

        void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return; // Left mouse button
            _isDragging = true;
            _startMousePosition = evt.mousePosition;
            _startSize = new(
                _imguiPreviewContainer.resolvedStyle.width,
                _imguiPreviewContainer.resolvedStyle.height
            );

            // Capture mouse to get events even when outside element
            _dragHandle.CaptureMouse();
            evt.StopPropagation();
        }

        void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_isDragging) return;
            Vector2 delta = evt.mousePosition - _startMousePosition;

            //_resizableContainer.style.width = Mathf.Max(50, _startSize.x + delta.x);
            _imguiPreviewContainer.style.height = Mathf.Max(50, _startSize.y + delta.y);

            evt.StopPropagation();
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            if (!_isDragging || evt.button != 0) return;
            _isDragging = false;
            _dragHandle.ReleaseMouse();

            _previewHeight = _imguiPreviewContainer.resolvedStyle.height;
            BetterMeshSettings.instance.MeshPreviewHeight = _previewHeight;
            evt.StopPropagation();
        }
    }
}