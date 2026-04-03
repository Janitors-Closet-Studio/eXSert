using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectFirstButtonInEntryList : MonoBehaviour
{
    [SerializeField] private RectTransform scrollRectTransform;
    [SerializeField] private RectTransform contentRectTransform;

    private Button buttonToSelect;

    private void OnEnable()
    {
        // Wait a frame to ensure all buttons are initialized
        StartCoroutine(SelectFirstButtonNextFrame());
    }

    private System.Collections.IEnumerator SelectFirstButtonNextFrame()
    {
        yield return null; // Wait one frame

        // Find the first active button in the content
        buttonToSelect = contentRectTransform.GetComponentInChildren<Button>(true);
        if (buttonToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(buttonToSelect.gameObject);
            ScrollToButton(buttonToSelect);
        }
    }

    private void ScrollToButton(Button button)
    {
        // Get the position of the button relative to the content
        Vector3 buttonLocalPos = button.transform.localPosition;
        float contentHeight = contentRectTransform.rect.height;
        float viewportHeight = scrollRectTransform.rect.height;

        // Calculate the normalized scroll position (0 at bottom, 1 at top)
        float normalizedPos = 1 - ((buttonLocalPos.y + (button.GetComponent<RectTransform>().rect.height / 2)) / (contentHeight - viewportHeight));

        // Clamp between 0 and 1
        normalizedPos = Mathf.Clamp01(normalizedPos);

        // Set the scroll position
        scrollRectTransform.GetComponent<ScrollRect>().verticalNormalizedPosition = normalizedPos;
    }
}
