using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections;
using UnityEngine.Events;

public class MenuEventSystemHandler : MonoBehaviour
{
    [Header("UI Selectables")]
    [Tooltip("List of UI Selectables (Buttons, Toggles, etc.) to add selection listeners to")]
    public List<Selectable> Selectables = new List<Selectable>();
    [SerializeField] internal Selectable _firstSelected;

    [Header("Input")]
    [SerializeField] protected InputActionReference _navigateReference;

    [Header("Animations")]
    [SerializeField] protected float _selectedAnimationScale = 1.0f;
    [SerializeField] protected float _scaleDuration = 0.25f;
    [SerializeField] protected List<GameObject> _animationExclusions = new List<GameObject>();

    [Header("Sounds")]
    [SerializeField] protected UnityEvent SoundEvent;

    // stores all the scales of all the UI Selectables
    protected Dictionary<Selectable, Vector3> _scales = new Dictionary<Selectable, Vector3>();

    protected Selectable _lastSelected;

    protected Tween _scaleUpTween;
    protected Tween _scaleDownTween;

    public virtual void Awake()
    {
        // store the scales of all the UI Selectables while setting up selection listeners
        foreach (var selectable in Selectables)
        {
            if (selectable != null && !_scales.ContainsKey(selectable))
            {
                AddSelectionListeners(selectable);
                _scales.Add(selectable, selectable.transform.localScale);
            }
            else
            {
                Debug.LogWarning("A selectable in the Selectables list is null or already exists in the dictionary.");
            }
        }
    }

    public virtual void OnEnable()
    {
        if (_navigateReference == null || _navigateReference.action == null)
            Debug.LogWarning($"Navigate Input Action Reference is not set in the inspector. Keyboard/Controller Input won't navigate menu \"{name}\" properly");

        else
            _navigateReference.action.performed += OnNavigate;


        // reset all the scales of all the UI Selectables
        for (int i = 0; i < Selectables.Count; i++)
        {
            if (Selectables[i] != null && _scales.ContainsKey(Selectables[i]))
                Selectables[i].transform.localScale = _scales[Selectables[i]];

            else
                Debug.LogWarning("A selectable in the Selectables list is null or does not exist in the dictionary.");
        }

        // set the first selected UI Selectable
        if (_firstSelected != null)
            StartCoroutine(SelectAfterDelay());

        // if first selected is not set, assign the first selectable in the list
        else
        {
            Debug.LogWarning("First Selected is not set. Assigning Default Value");
            if (Selectables.Count > 0 && Selectables[0] != null)
            {
                _firstSelected = Selectables[0];
                StartCoroutine(SelectAfterDelay());
            }
            else
            {
                Debug.LogError("No valid selectable found to set as first selected.");
            }
        }
    }

    protected virtual IEnumerator SelectAfterDelay()
    {
        yield return null;

        // Ensure there's an EventSystem to use. When scenes load, EventSystem.current can be null for a frame.
        if (EventSystem.current == null)
        {
            EventSystem found = FindObjectOfType<EventSystem>();
            if (found != null)
                EventSystem.current = found;
        }

        if (EventSystem.current == null)
        {
            Debug.LogError($"No EventSystem found in scene to select first selectable for menu \"{name}\".");
            yield break;
        }

        if (_firstSelected == null)
        {
            Debug.LogError($"First selected is null for menu \"{name}\".");
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(_firstSelected.gameObject);
    }

    public virtual void OnDisable()
    {
        if (_navigateReference == null || _navigateReference.action == null)
            Debug.LogWarning($"Navigate Input Action Reference is not set in the inspector. Keyboard/Controller Input won't navigate menu \"{name}\" properly");

        else
            _navigateReference.action.performed -= OnNavigate;


        _scaleUpTween?.Kill();
        _scaleDownTween?.Kill();
    }

    /* adds an EventTrigger to each selectable to handle selection and deselection events
     * it will create an EventTrigger component if one does not already exist
     * then it adds listeners to the SELECT and DESELECT events
     */
    protected virtual void AddSelectionListeners(Selectable selectable)
    {
        // add listener to selectable
        EventTrigger trigger = selectable.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            // if there is no EventTrigger component, add one
            trigger = selectable.gameObject.AddComponent<EventTrigger>();
        }

        // add SELECT event
        EventTrigger.Entry SelectEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Select
        };
        SelectEntry.callback.AddListener(OnSelect);
        trigger.triggers.Add(SelectEntry);

        // add DESELECT event
        EventTrigger.Entry DeselectEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Deselect
        };
        DeselectEntry.callback.AddListener(OnDeselect);
        trigger.triggers.Add(DeselectEntry);

        // add ONPOINTERENTER event (to handle mouse hover selection)
        EventTrigger.Entry PointerEnter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        PointerEnter.callback.AddListener(OnPointerEnter);
        trigger.triggers.Add(PointerEnter);

        // add ONPOINTEREXIT event (to handle mouse hover deselection)
        EventTrigger.Entry PointerExit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        PointerExit.callback.AddListener(OnPointerExit);
        trigger.triggers.Add(PointerExit);
    }

    public void OnSelect(BaseEventData eventData)
    {
        // play sound
        SoundEvent?.Invoke();


        _lastSelected = eventData.selectedObject.GetComponent<Selectable>();

        // if the selected object is in the exclusions list, do not animate
        if (_animationExclusions.Contains(eventData.selectedObject))
            return;


        Vector3 newScale = eventData.selectedObject.transform.localScale * _selectedAnimationScale;
        _scaleUpTween = eventData.selectedObject.transform.DOScale(newScale, _scaleDuration);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        // if the deselected object is in the exclusions list, do not animate
        if (_animationExclusions.Contains(eventData.selectedObject))
            return;


        Selectable sel = eventData.selectedObject.GetComponent<Selectable>();
        _scaleDownTween = eventData.selectedObject.transform.DOScale(_scales[sel], _scaleDuration);
    }

    public void OnPointerEnter(BaseEventData eventData)
    {
        PointerEventData pointerEventData = eventData as PointerEventData;
        if (pointerEventData != null)
        {
            Selectable sel = pointerEventData.pointerEnter.GetComponentInParent<Selectable>();
            if (sel == null)
            {
                sel = pointerEventData.pointerEnter.GetComponentInChildren<Selectable>();
            }

            pointerEventData.selectedObject = sel.gameObject;
        }
    }

    public void OnPointerExit(BaseEventData eventData)
    {
        PointerEventData pointerEventData = eventData as PointerEventData;
        if (pointerEventData != null)
        {
            pointerEventData.selectedObject = null;
        }
    }

    protected virtual void OnNavigate(InputAction.CallbackContext context)
    {
        // Guard against missing EventSystem (can be null for frames during scene load)
        if (EventSystem.current == null)
            return;

        if (EventSystem.current.currentSelectedGameObject == null && _lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(_lastSelected.gameObject);
        }
    }
}