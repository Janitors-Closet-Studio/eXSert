#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TinyGiantStudio.BetterInspector
{
#if BETTEREDITOR_USE_PROJECTSETTINGS
    [FilePath("ProjectSettings/Tiny Giant Studio/Better Editor/Scales.asset", FilePathAttribute.Location.ProjectFolder)]
#else
    [FilePath("UserSettings/Tiny Giant Studio/Better Editor/Scales.asset", FilePathAttribute.Location.ProjectFolder)]
#endif
    public class ScalesManager : ScriptableSingleton<ScalesManager>
    {
        [FormerlySerializedAs("_selectedUnit")] [SerializeField]
        public int
            selectedUnit; //This is public to support importing new Better Mesh to the older version of BetterTransform.

        //Ignore the naming scheme for now
        public int
            SelectedUnit //This is currently only used by Better Transform. Better mesh saves the selected unit in its own settings file
        {
            get => selectedUnit;
            set
            {
                selectedUnit = value;
                Save(true);
            }
        }

        [FormerlySerializedAs("_units")] [SerializeField]
        List<Unit> units = new();

        public List<Unit> Units
        {
            get => units;
            private set
            {
                units = value;
                Save(true);
            }
        }

        /// <summary>
        /// This is currently only used by Better Transform. Better mesh saves the selected unit in its own settings file
        /// </summary>
        public float CurrentUnitValue() => UnitValue(SelectedUnit);

        public float UnitValue(int unit)
        {
            if (unit < 0 || unit > Units.Count) return 0; //for invalid value

            return Units[unit].value;
        }

        public string[] GetAvailableUnits()
        {
            string[] availableUnits = new string[Units.Count];
            for (int i = 0; i < availableUnits.Length; i++)
            {
                availableUnits[i] = Units[i].name;
            }

            return availableUnits;
        }

        [ContextMenu("Reset")]
        public void Reset()
        {
            SelectedUnit = 0;
            Units = new()
            {
                new("Meter", 1),
                new("Kilometer", 0.001f),
                new("Centimeter", 100),
                new("Millimeter", 1000),
                new("Feet", 3.28084f),
                new("Inch", 39.3701f),
                new("Yards", 1.09f),
                new("Miles", 0.00062f),
                new("NauticalMile", 0.000539957f),
                new("Banana", 5.618f)
            };

            EditorUtility.SetDirty(this);
        }
    }

    [System.Serializable]
    public class Unit
    {
        public string name;

        /// <summary>
        /// This is the value of the unit compared to meter, which is the default unity scale
        /// </summary>
        public float value;

        public Unit(string name, float value)
        {
            this.name = name;
            this.value = value;
        }
    }
}
#endif