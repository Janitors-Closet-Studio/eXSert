using UnityEngine;
using TMPro;
public class TestForWritingText : MonoBehaviour
{
    private TextMeshProUGUI textComponent;
    private WritingTextUI.TextWriterSingle textWriterSingle;
    private void Awake()
    {
        textComponent = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (textWriterSingle != null && textWriterSingle.IsActive())
        {
            textWriterSingle.WriteAllAndDestroy();
        }
        else
        {

        WritingTextUI.AddWriter_Static(textComponent, "Hello, this is a test for writing text character by character.", 0.1f, true, true);
        WritingTextUI.AddWriter_Static(textComponent, "This is the second line of text that will appear after the first one.", 0.1f, true, false);
        }
    }
}
