using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISelectOutline : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler
{
    [Header("Outline")]

    [SerializeField]
    private GameObject focusOutline;

    [Header("Text References")]
    [SerializeField, Tooltip("TextMeshPro text components to modify on selection")]
    private TextMeshProUGUI[] textComponents;

    [Header("Colors")]

    [SerializeField]
    private Color defaultColor = new Color(0.263f, 0.263f, 0.263f, 1f); // #434343

    [SerializeField]
    private Color selectedColor = new Color(0.898f, 0.898f, 0.898f, 1f); // #E5E5E5

    // Gamepad/keyboard selection
    public void OnSelect(BaseEventData eventData) => SetVisualState(true);

    public void OnDeselect(BaseEventData eventData) => SetVisualState(false);

    // Mouse hover (optional)
    public void OnPointerEnter(PointerEventData e) => SetVisualState(true);

    public void OnPointerExit(PointerEventData e) => SetVisualState(false);

    private void OnDisable()
    {
        SetVisualState(false);
    }

    private void SetVisualState(bool selected)
    {
        Selectable selectable = GetComponent<Selectable>();
        if (selected && selectable != null && !selectable.IsInteractable())
            return;
            
        // Toggle outline
        if (focusOutline)
            focusOutline.SetActive(selected);

        // Change text color
        if (textComponents != null)
        {
            Color targetColor = selected ? selectedColor : defaultColor;

            foreach (var text in textComponents)
            {
                if (text != null)
                {
                    text.color = targetColor;
                }
            }
        }
    }
}
