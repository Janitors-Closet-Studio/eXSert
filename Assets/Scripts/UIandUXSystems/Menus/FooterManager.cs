using TMPro;
using UnityEngine;

public class FooterManager : MonoBehaviour
{
    [SerializeField] private TMP_Text footerText;
    [SerializeField] private string defaultFooterMessage = "Explore Your Settings";

    private void OnEnable()
    {
        UpdateFooterText(defaultFooterMessage);
    }

    public void UpdateFooterText(string message)
    {
        if (footerText != null)
        {
            footerText.text = message;
        }
    }


}

