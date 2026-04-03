using UnityEngine;
using UnityEngine.EventSystems;

public class SelectObjectOnBack : MonoBehaviour
{
    [SerializeField] private GameObject objectToSelectOnBack;

    private void OnDisable()
    {
        if (EventSystem.current != null)
        {
            SelectOnBack();
        }
       
    }

    public void SelectOnBack()
    {
        if (objectToSelectOnBack != null && objectToSelectOnBack.activeInHierarchy)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null); // Clear current selection
                EventSystem.current.SetSelectedGameObject(objectToSelectOnBack); // Set new selection
            }
        }
    }
}
