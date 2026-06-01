using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollRectAutoScroll : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float scrollSpeed = 10f;
    private bool mouseOver = false;

    private List<Selectable> m_Selectables = new List<Selectable>();
    private ScrollRect m_ScrollRect;
    private RectTransform m_ContentRect;
    private RectTransform m_ViewportRect;
    private GameObject m_LastSelectedObject;
    private int m_LastChildCount = -1;

    private Vector2 m_NextScrollPosition = Vector2.up;

    void OnEnable()
    {
        RefreshSelectables(true);
    }

    void Awake()
    {
        m_ScrollRect = GetComponent<ScrollRect>();
        m_ContentRect = m_ScrollRect.content;
        m_ViewportRect = m_ScrollRect.viewport != null ? m_ScrollRect.viewport : transform as RectTransform;
    }

    void Start()
    {
        RefreshSelectables(true);
        ScrollToSelected(true);
    }

    void Update()
    {
        RefreshSelectables(false);

        if (SelectionChanged())
        {
            ScrollToSelected(false);
        }

        if (!mouseOver)
        {
            m_ScrollRect.normalizedPosition = Vector2.Lerp(m_ScrollRect.normalizedPosition, m_NextScrollPosition, scrollSpeed * Time.unscaledDeltaTime);

            if (ShouldKeepSelectedVisible())
            {
                ScrollToSelected(false);
            }
        }
        else
        {
            m_NextScrollPosition = m_ScrollRect.normalizedPosition;
        }
    }

    void RefreshSelectables(bool forceRebuild)
    {
        if (m_ScrollRect == null || m_ContentRect == null)
        {
            return;
        }

        int childCount = m_ContentRect.childCount;
        if (!forceRebuild && childCount == m_LastChildCount)
        {
            return;
        }

        m_LastChildCount = childCount;
        m_Selectables.Clear();

        Selectable[] selectables = m_ContentRect.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable != null && selectable.gameObject.activeInHierarchy)
            {
                m_Selectables.Add(selectable);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    bool SelectionChanged()
    {
        GameObject currentSelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (currentSelectedObject == m_LastSelectedObject)
        {
            return false;
        }

        m_LastSelectedObject = currentSelectedObject;
        return true;
    }

    bool ShouldKeepSelectedVisible()
    {
        RectTransform selectedRect = GetSelectedRectTransform();
        if (selectedRect == null)
        {
            return false;
        }

        Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(m_ViewportRect, selectedRect);
        Rect viewportRect = m_ViewportRect.rect;

        return selectedBounds.max.y > viewportRect.yMax || selectedBounds.min.y < viewportRect.yMin;
    }

    void ScrollToSelected(bool quickScroll)
    {
        RectTransform selectedRect = GetSelectedRectTransform();
        if (selectedRect == null || m_ContentRect == null || m_ViewportRect == null)
        {
            return;
        }

        float hiddenLength = m_ContentRect.rect.height - m_ViewportRect.rect.height;
        if (hiddenLength <= 0f)
        {
            m_NextScrollPosition = new Vector2(m_ScrollRect.horizontalNormalizedPosition, 1f);
            return;
        }

        Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(m_ViewportRect, selectedRect);
        Rect viewportRect = m_ViewportRect.rect;

        float targetAnchoredY = m_ContentRect.anchoredPosition.y;

        if (selectedBounds.max.y > viewportRect.yMax)
        {
            targetAnchoredY -= selectedBounds.max.y - viewportRect.yMax;
        }
        else if (selectedBounds.min.y < viewportRect.yMin)
        {
            targetAnchoredY += viewportRect.yMin - selectedBounds.min.y;
        }
        else if (!quickScroll)
        {
            return;
        }

        targetAnchoredY = Mathf.Clamp(targetAnchoredY, 0f, hiddenLength);

        Vector2 targetPosition = new Vector2(
            m_ScrollRect.horizontalNormalizedPosition,
            1f - Mathf.Clamp01(targetAnchoredY / hiddenLength));

        if (quickScroll)
        {
            m_ScrollRect.normalizedPosition = targetPosition;
        }

        m_NextScrollPosition = targetPosition;
    }

    RectTransform GetSelectedRectTransform()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return null;
        }

        Selectable selectedElement = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
        if (selectedElement == null || !m_Selectables.Contains(selectedElement))
        {
            return null;
        }

        return selectedElement.transform as RectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        mouseOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseOver = false;
        ScrollToSelected(false);
    }
}