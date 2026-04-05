using TMPro;
using UnityEngine;
using System.Collections;

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
            // Use WritingTextUI for typewriter effect
            if (HUDText != null)
            {
                // Example: 0.03f seconds per character, invisibleCharacters = false
                WritingTextUI.AddWriter_Static(HUDText, newObjective, 0.03f, false);
            }
            else
            {
                UpdateText();
            }
        }

        private void UpdateText()
        {
            if (HUDText == null)
                return;

            // Fallback: set text directly if not using typewriter effect
            HUDText.text = currentMessage;
        }

        public void FadeOutText(float delay)
        {
            StartCoroutine(FadeOutObjectiveText(delay));
        }

        private IEnumerator FadeOutObjectiveText(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (HUDText != null)
            {
                float alpha = 1f;
                while (alpha > 0f)
                {
                    alpha -= Time.deltaTime / 0.5f; // Fade out over 0.5 seconds
                    HUDText.color = new Color(HUDText.color.r, HUDText.color.g, HUDText.color.b, alpha);
                    yield return null;
                }
                HUDText.text = ""; // Clear text after fade out
            }
        }
    }
}
