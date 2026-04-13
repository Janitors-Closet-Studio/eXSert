using System.Collections;
using TMPro;
using UnityEngine;

internal class ObjectiveText : MonoBehaviour
{
    [SerializeField, CriticalReference]
    private TextMeshProUGUI HUDText;

    [SerializeField]
    private float typingSpeed = 0.03f;

    private Objective currentMessage;
    private string currentMessageString => currentMessage?.DisplayText ?? "";

    private void OnEnable()
    {
        ObjectiveManager.OnObjectiveChanged += UpdateText;
    }

    private void OnDisable()
    {
        ObjectiveManager.OnObjectiveChanged -= UpdateText;
    }

    private void UpdateText(Objective newObjective)
    {
        Debug.Log($"[HUDTextHandler] Setting new message: {newObjective}");
        currentMessage = newObjective;

        WritingTextUI.AddWriter_Static(HUDText, currentMessageString, typingSpeed, false);
    }

    // Probably remove below. I'm keeping it for now in case it is actually important

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
