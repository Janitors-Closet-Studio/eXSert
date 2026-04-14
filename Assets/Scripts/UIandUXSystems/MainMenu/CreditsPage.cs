using UnityEngine;
using UnityEngine.InputSystem;

public class CreditsPage : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference leftInputAction;
    [SerializeField] private InputActionReference rightInputAction;

    [Header("Page GameObjects")]
    [SerializeField] private GameObject[] creditPages;

    [SerializeField] private int startingPageIndex = 0;
    [SerializeField] private int lastPageIndex = 1;
    public int currentPageIndex;


    private void OnEnable()
    {
        if (leftInputAction != null)
        {
            leftInputAction.action.Enable();
            leftInputAction.action.performed += FlipLeft;
            Debug.Log("Left input action enabled and listener added.");
        }
        if (rightInputAction != null)
        {
            rightInputAction.action.Enable();
            rightInputAction.action.performed += FlipRight;
            Debug.Log("Right input action enabled and listener added.");
        }
    }

    private void OnDisable()
    {
        RestartPage();
        if (leftInputAction != null)
        {
            leftInputAction.action.performed -= FlipLeft;
            leftInputAction.action.Disable();
        }
        if (rightInputAction != null)
        {
            rightInputAction.action.performed -= FlipRight;
            rightInputAction.action.Disable();
        }
    }

    private void Start()
    {
        currentPageIndex = startingPageIndex;
        ShowPage(startingPageIndex, lastPageIndex);
    }

    
    private void ShowPage(int pageToShow, int pageToHide)
    {
        if (pageToShow < 0 || pageToShow >= creditPages.Length || pageToHide < 0 || pageToHide >= creditPages.Length)
        {
            Debug.LogError("Invalid page index. Cannot show/hide pages.");
            return;
        }

        creditPages[pageToShow].SetActive(true);
        creditPages[pageToHide].SetActive(false);
    }

    private void FlipLeft(InputAction.CallbackContext context)
    {
        if (currentPageIndex == 0)
        {
            Debug.Log("Already at the first page. Cannot flip left.");
            return;
        }

        int previousPageIndex = currentPageIndex;
        currentPageIndex--;
        ShowPage(currentPageIndex, previousPageIndex);
        lastPageIndex = previousPageIndex;
    }

    private void FlipRight(InputAction.CallbackContext context)
    {
        if (currentPageIndex == creditPages.Length - 1)
        {
            Debug.Log("Already at the last page. Cannot flip right.");
            return;
        }

        int previousPageIndex = currentPageIndex;
        currentPageIndex++;
        ShowPage(currentPageIndex, previousPageIndex);
        lastPageIndex = previousPageIndex;
    }

    private void RestartPage()
    {
        ShowPage(currentPageIndex, lastPageIndex);
    }
}
