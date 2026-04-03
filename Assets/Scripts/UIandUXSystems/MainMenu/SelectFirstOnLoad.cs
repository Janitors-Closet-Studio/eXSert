using UnityEngine;
using UnityEngine.EventSystems;

public class SelectFirstOnLoad : MonoBehaviour
{
    public GameObject firstSelectedObject; // Optional: assign in inspector for specific selection

    private void Start()
    {
        SelectFirstButton();
    }

    private void OnEnable()
    {
        // Use coroutine to delay selection by one frame for UI stability
        StartCoroutine(SelectFirstButtonNextFrame());
        
    }

    private System.Collections.IEnumerator SelectFirstButtonNextFrame()
        {
            yield return null; // Wait one frame
            SelectFirstButton();
        }

    // Allow other scripts to force selection (e.g., after returning to menu)
    public void ReselectFirstButton()
    {
        SelectFirstButton();
    }

    private EventSystem FindEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogWarning("No EventSystem found in the scene. Please add one for UI navigation to work.");
        }

        return UnityEngine.EventSystems.EventSystem.current;
    }

    private void SelectFirstButton()
    {
        EventSystem eventSystem = FindEventSystem();
        if (eventSystem == null)
            return;
        eventSystem.SetSelectedGameObject(null); // Clear any existing selection

        GameObject toSelect = null;
        if (eventSystem.firstSelectedGameObject != null && eventSystem.firstSelectedGameObject.activeInHierarchy && firstSelectedObject == null)
        {
            toSelect = eventSystem.firstSelectedGameObject;
        }
        else if (firstSelectedObject != null && firstSelectedObject.activeInHierarchy)
        {
            toSelect = firstSelectedObject;
        }
        else
        {
            // Fallback: find any active/interactable Button
            var anyButton = GameObject.FindObjectOfType<UnityEngine.UI.Button>();
            if (anyButton != null && anyButton.interactable && anyButton.gameObject.activeInHierarchy)
                toSelect = anyButton.gameObject;
        }
        if (toSelect != null)
        {
            eventSystem.SetSelectedGameObject(toSelect);
            Debug.Log($"[SelectFirstOnLoad] Selected first selectable: {toSelect.name}");
        }
        else
        {
            Debug.LogWarning("No selectable Button UI element found in the scene to select.");
        }
    }
}
