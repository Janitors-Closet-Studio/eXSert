using UnityEngine;
using UnityEngine.EventSystems;

public class SelectObjectOnBack : MonoBehaviour
{
    [SerializeField] private GameObject objectToSelectOnBack;

    private void OnDisable()
    {
        if (MenuSelectionSuppression.IsSuppressed)
            return;

        if (EventSystem.current != null)
        {
            SelectOnBack();
        }
       
    }

    public void SelectOnBack()
    {
        if (MenuSelectionSuppression.IsSuppressed)
            return;

        if (objectToSelectOnBack != null && objectToSelectOnBack.activeInHierarchy)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(objectToSelectOnBack); // Set new selection
            }
        }
    }
}
