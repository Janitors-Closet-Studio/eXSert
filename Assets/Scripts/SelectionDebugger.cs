using UnityEngine;
using UnityEngine.EventSystems;

public static class SelectionDebugger
{
    public static void SetSelected(GameObject obj)
    {
        string objName = obj ? obj.name : "<null>";
        Debug.Log($"[SelectionDebugger] Forcing selection: {objName}\nStackTrace:\n" + System.Environment.StackTrace);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(obj);
    }
}
