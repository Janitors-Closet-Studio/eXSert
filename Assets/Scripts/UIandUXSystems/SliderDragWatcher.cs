using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Attach this to your slider prefab or GameObject
public class SliderDragWatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static bool SliderIsBeingDragged = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        SliderIsBeingDragged = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SliderIsBeingDragged = false;
    }
}
