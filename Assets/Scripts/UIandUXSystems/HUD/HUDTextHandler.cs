using TMPro;
using UnityEngine;

namespace UIandUXSystems.HUD
{
    internal abstract class HUDTextHandler : MonoBehaviour
    {
        [SerializeField, CriticalReference]
        private TextMeshProUGUI HUDText;

        internal abstract HUDMessageType HUDIdentifier { get; }

        private string currentMessage = "Objective Text";

        private void Awake()
        {
            PlayerHUD.RegisterHUDHandler(this);

            UpdateText();
        }

        internal void SetText(string newObjective)
        {
            Debug.Log($"[HUDTextHandler] Setting new {HUDIdentifier} message: {newObjective}");
            currentMessage = newObjective;
            UpdateText();
        }

        private void UpdateText()
        {
            if (HUDText == null)
                return;

            HUDText.text = currentMessage;
        }
    }
}
