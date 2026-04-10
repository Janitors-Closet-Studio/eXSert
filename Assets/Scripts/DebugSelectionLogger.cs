using UnityEngine;
using UnityEngine.EventSystems;

public class DebugSelectionLogger : MonoBehaviour
{
    private GameObject lastSelected;

    void Update()
    {
        if (EventSystem.current == null) return;
        var current = EventSystem.current.currentSelectedGameObject;
        if (current != lastSelected)
        {
            string prev = lastSelected ? lastSelected.name : "<null>";
            string curr = current ? current.name : "<null>";
            Debug.Log($"[DebugSelectionLogger] Selection changed: {prev} -> {curr}\nStackTrace:\n" + System.Environment.StackTrace, this);
            lastSelected = current;
        }
    }
}
