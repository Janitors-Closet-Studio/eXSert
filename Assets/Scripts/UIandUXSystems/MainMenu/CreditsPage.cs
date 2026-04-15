using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CreditsPage : MonoBehaviour
    
{
    private bool isTransitioning = false;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference leftInputAction;
    [SerializeField] private InputActionReference rightInputAction;

    [Header("Page GameObjects")]
    [SerializeField] private GameObject[] creditPages;
    [SerializeField] private TextMeshProUGUI pageIndicatorText;
    [SerializeField] private TextMeshProUGUI amountOfPagesText;

    [SerializeField] private int startingPageIndex = 0;
    [SerializeField] private int lastPageIndex = 1;
    public int currentPageIndex;


    private void OnEnable()
           
    {

        isTransitioning = false;
        // Always show the first page when entering
        currentPageIndex = startingPageIndex;
        lastPageIndex = startingPageIndex;
        for (int i = 0; i < creditPages.Length; i++)
        {
            var cg = creditPages[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = creditPages[i].AddComponent<CanvasGroup>();
            if (i == startingPageIndex)
            {
                cg.alpha = 1f;
                creditPages[i].SetActive(true);
            }
            else
            {
                cg.alpha = 0f;
                creditPages[i].SetActive(false);
            }
        }
        if (pageIndicatorText != null)
            pageIndicatorText.text = (startingPageIndex + 1).ToString();
        if (amountOfPagesText != null)
            amountOfPagesText.text = creditPages.Length.ToString();

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
        if (pageIndicatorText != null)
            pageIndicatorText.text = (startingPageIndex + 1).ToString();
        if (amountOfPagesText != null)
            amountOfPagesText.text = creditPages.Length.ToString();
    }

    
    private void ShowPage(int pageToShow, int pageToHide)
    {
        if (pageToShow < 0 || pageToShow >= creditPages.Length || pageToHide < 0 || pageToHide >= creditPages.Length)
        {
            Debug.LogError("Invalid page index. Cannot show/hide pages.");
            return;
        }
        if (pageToShow == pageToHide)
        {
            creditPages[pageToShow].SetActive(true);
            var cg = creditPages[pageToShow].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            return;
        }
        StartCoroutine(MasterFadeCoroutine(creditPages[pageToShow], creditPages[pageToHide], 0.5f));
        creditPages[pageToShow].SetActive(true);
        creditPages[pageToHide].SetActive(false);
    }

    private void FlipLeft(InputAction.CallbackContext context)
    {
        if (isTransitioning) return;
        if (currentPageIndex == 0)
        {
            Debug.Log("Already at the first page. Cannot flip left.");
            return;
        }

        int previousPageIndex = currentPageIndex;
        currentPageIndex--;
        StartCoroutine(PageTransitionCoroutine(currentPageIndex, previousPageIndex));
        lastPageIndex = previousPageIndex;

        if (pageIndicatorText != null)
            pageIndicatorText.text = (currentPageIndex + 1).ToString();

        
    }

    private void FlipRight(InputAction.CallbackContext context)
    {
        if (isTransitioning) return;
        if (currentPageIndex == creditPages.Length - 1)
        {
            Debug.Log("Already at the last page. Cannot flip right.");
            return;
        }

        int previousPageIndex = currentPageIndex;
        currentPageIndex++;
        StartCoroutine(PageTransitionCoroutine(currentPageIndex, previousPageIndex));
        lastPageIndex = previousPageIndex;

        if (pageIndicatorText != null)
            pageIndicatorText.text = (currentPageIndex + 1).ToString();
    

        StartCoroutine(MasterFadeCoroutine(creditPages[currentPageIndex], creditPages[previousPageIndex], 0.5f));
    }

    private IEnumerator PageTransitionCoroutine(int newPageIndex, int oldPageIndex)
    {
        isTransitioning = true;
        yield return StartCoroutine(MasterFadeCoroutine(creditPages[newPageIndex], creditPages[oldPageIndex], 0.45f));
        isTransitioning = false;
    }

    private void RestartPage()
    {
        for (int i = 0; i < creditPages.Length; i++)
        {
            var cg = creditPages[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = creditPages[i].AddComponent<CanvasGroup>();
            if (i == startingPageIndex)
            {
                cg.alpha = 1f;
                creditPages[i].SetActive(true);
            }
            else
            {
                cg.alpha = 0f;
                creditPages[i].SetActive(false);
            }
        }
        if (pageIndicatorText != null)
            pageIndicatorText.text = (startingPageIndex + 1).ToString();
    }

    private IEnumerator MasterFadeCoroutine(GameObject pageToFadeIn, GameObject pageToFadeOut, float fadeDuration)
    {
        // Ensure the new page is active and alpha is 0 before fading in
        var inCanvasGroup = pageToFadeIn.GetComponent<CanvasGroup>();
        if (inCanvasGroup == null)
            inCanvasGroup = pageToFadeIn.AddComponent<CanvasGroup>();
        inCanvasGroup.alpha = 0f;
        pageToFadeIn.SetActive(true);

        // Fade out the old page
        yield return StartCoroutine(FadeOutPage(pageToFadeOut, fadeDuration));

        // Fade in the new page
        yield return StartCoroutine(FadeInPage(pageToFadeIn, fadeDuration));

        // Optionally, deactivate the old page after fade out
        pageToFadeOut.SetActive(false);
    }

    private IEnumerator FadeInPage(GameObject pageToFade, float fadeDuration)
    {
        CanvasGroup canvasGroup = pageToFade.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogError("No CanvasGroup found on the page to fade.");
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutPage(GameObject pageToFade, float fadeDuration)
    {
        CanvasGroup canvasGroup = pageToFade.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogError("No CanvasGroup found on the page to fade.");
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
