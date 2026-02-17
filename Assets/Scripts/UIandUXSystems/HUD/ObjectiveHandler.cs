using TMPro;
using UnityEngine;

namespace UIandUXSystems.HUD
{
    internal class ObjectiveHandler : MonoBehaviour
    {
        [SerializeField, CriticalReference]
        private TextMeshProUGUI objectiveText;

        private string currentObjective = "Objective Text";

        private void Start()
        {
            PlayerHUD.objectiveHandler = this;

            UpdateObjectiveText();
        }

        public void SetObjective(string newObjective)
        {
            currentObjective = newObjective;
            UpdateObjectiveText();
        }

        private void UpdateObjectiveText()
        {
            if (objectiveText == null)
                return;

            objectiveText.text = currentObjective;
        }
    }
}
