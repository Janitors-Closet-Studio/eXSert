using UnityEngine;
using TMPro;

public class LoadSaveTextChanger : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI footerText;

    [Header("New Game Text")]
    [SerializeField] private string newGameTitle = "NEW GAME";
    [SerializeField] private string newGameFooter = "Select a Save File";

    [Header("Load Game Text")]
    [SerializeField] private string loadGameTitle = "LOAD GAME";
    [SerializeField] private string loadGameFooter = "Select a Save File to Load";

    public void OnNewGameSelected()
    {
        titleText.text = newGameTitle;
        footerText.text = newGameFooter;
    }

    public void OnLoadGameSelected()
    {
        titleText.text = loadGameTitle;
        footerText.text = loadGameFooter;
    }
}
