using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    public class BaseSizeFoldoutManager
    {
        readonly GroupBox _sizeFoldout;
        readonly BetterMeshSettings _editorSettings;

        public BaseSizeFoldoutManager(BetterMeshSettings editorSettings, VisualElement root)
        {
            _editorSettings = editorSettings;

            _sizeFoldout = root.Q<GroupBox>("MeshSize");
            CustomFoldout.SetupFoldout(_sizeFoldout);
        }

        public void HideFoldout()
        {
            _sizeFoldout.style.display = DisplayStyle.None;
        }

        public void UpdateTargets(List<Mesh> meshes)
        {
            if (meshes.Count != 1 || !_editorSettings.ShowSizeFoldout)
            {
                _sizeFoldout.style.display = DisplayStyle.None;
                return;
            }

            _sizeFoldout.style.display = DisplayStyle.Flex;

            Mesh newMesh = meshes[0];
            if (newMesh == null) return;

            DropdownField meshUnitDropdown = _sizeFoldout.parent.Q<DropdownField>("MeshUnit");

            ScalesManager scalesManager = ScalesManager.instance;
            if (scalesManager.GetAvailableUnits().ToList().Count == 0) scalesManager.Reset();
            meshUnitDropdown.choices = scalesManager.GetAvailableUnits().ToList();

            meshUnitDropdown.index = _editorSettings.SelectedUnit;

            meshUnitDropdown.RegisterCallback<ChangeEvent<string>>(_ =>
            {
                _editorSettings.SelectedUnit = meshUnitDropdown.index;
                UpdateValues();
            });

            UpdateValues();
            return;

            void UpdateValues()
            {
                _editorSettings.SelectedUnit = meshUnitDropdown.index;
                Bounds meshBound = newMesh.MeshSizeEditorOnly(scalesManager.UnitValue(_editorSettings.SelectedUnit));

                GroupBox meshSizeTemplateContainer = _sizeFoldout.Q<GroupBox>("MeshSize");

                GroupBox lengthGroup = meshSizeTemplateContainer.Q<GroupBox>("LengthGroup");
                Label lengthValue = lengthGroup.Q<Label>("Value");
                lengthValue.text = RoundedFloat(meshBound.size.x).ToString(CultureInfo.InvariantCulture);
                lengthValue.tooltip = meshBound.size.x.ToString(CultureInfo.InvariantCulture);

                GroupBox heightGroup = meshSizeTemplateContainer.Q<GroupBox>("HeightGroup");
                Label heightValue = heightGroup.Q<Label>("Value");
                heightValue.text = RoundedFloat(meshBound.size.y).ToString(CultureInfo.InvariantCulture);
                heightValue.tooltip = meshBound.size.y.ToString(CultureInfo.InvariantCulture);

                GroupBox depthGroup = meshSizeTemplateContainer.Q<GroupBox>("DepthGroup");
                Label depthValue = depthGroup.Q<Label>("Value");
                depthValue.text = RoundedFloat(meshBound.size.z).ToString(CultureInfo.InvariantCulture);
                depthValue.tooltip = meshBound.size.z.ToString(CultureInfo.InvariantCulture);

                Label centerLabel = _sizeFoldout.Q<Label>("Center");
                string centerText = RoundedFloat(meshBound.center.x) + ", " + RoundedFloat(meshBound.center.y) + ", " + RoundedFloat(meshBound.center.z);
                centerLabel.text = centerText;
                centerLabel.tooltip = "Number is rounded after 4 digits";
            }

            float RoundedFloat(float rawFloat) => (float)System.Math.Round(rawFloat, 4);
        }
    }
}