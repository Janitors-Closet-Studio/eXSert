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
        if (MenuSelectionSuppression.IsSuppressed)
            return;

        // Wait a frame to ensure all buttons are initialized
        StartCoroutine(SelectFirstButtonNextFrame());
    }

    private System.Collections.IEnumerator SelectFirstButtonNextFrame()
    {
        // Wait a couple of frames so dynamic entries/layout groups finish rebuilding.
        yield return null;
        yield return null;

        if (MenuSelectionSuppression.IsSuppressed)
            yield break;

        ScrollRect scrollRect = scrollRectTransform != null ? scrollRectTransform.GetComponent<ScrollRect>() : null;
        if (scrollRect != null)
        {
            // Always start from the top of the stack when the menu opens.
            scrollRect.verticalNormalizedPosition = 1f;
        }

        if (contentRectTransform != null)
            contentRectTransform.anchoredPosition = new Vector2(contentRectTransform.anchoredPosition.x, 0f);

        // Find the first active button in content sibling order.
        buttonToSelect = FindFirstActiveButtonInContent();
        if (buttonToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(buttonToSelect.gameObject);
        }
    }

    private Button FindFirstActiveButtonInContent()
    {
        if (contentRectTransform == null)
            return null;

        for (int i = 0; i < contentRectTransform.childCount; i++)
        {
            Transform child = contentRectTransform.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            Button directButton = child.GetComponent<Button>();
            if (directButton != null && directButton.interactable)
                return directButton;

            Button nestedButton = child.GetComponentInChildren<Button>(true);
            if (nestedButton != null && nestedButton.gameObject.activeInHierarchy && nestedButton.interactable)
                return nestedButton;
        }

        return null;
    }
}
