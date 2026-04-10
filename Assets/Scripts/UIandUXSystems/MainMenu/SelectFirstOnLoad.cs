using UnityEngine;
using UnityEngine.EventSystems;

public class SelectFirstOnLoad : MonoBehaviour
{
    public GameObject firstSelectedObject; // Optional: assign in inspector for specific selection

    private void Start()
    {
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

        // Only set selection if nothing is currently selected or the selection is inactive
        var current = eventSystem.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy)
        {
            // Something is already selected and active, do nothing
            return;
        }

        GameObject toSelect = null;
        if (firstSelectedObject != null && firstSelectedObject.activeInHierarchy)
        {
            toSelect = firstSelectedObject;
        }
        else if (eventSystem.firstSelectedGameObject != null && eventSystem.firstSelectedGameObject.activeInHierarchy)
        {
            toSelect = eventSystem.firstSelectedGameObject;
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
