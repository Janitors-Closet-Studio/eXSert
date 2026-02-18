using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class LoadFirstEntry : MonoBehaviour
{
    [SerializeField] internal GameObject playerHud;
    [SerializeField] private AudioClip entryFoundClip;

    public IEnumerator ShowScreenIfFirstEntry(GameObject entryUI, GameObject playerHud, MonoBehaviour scrollingList, 
     GameObject content)
    {
        var navigationMenuGO = NavigationMenu.Instance != null ? NavigationMenu.Instance.navigationMenuGO : null;
        if (navigationMenuGO == null)
        {
            Debug.LogError("NavigationMenu.Instance or navigationMenuGO is null in ShowScreenIfFirstEntry");
            yield break;
        }

        var childOne = navigationMenuGO.transform.GetChild(0).gameObject;
        var ui = entryUI.gameObject;

        childOne.SetActive(true);
        ui.SetActive(true);
        playerHud.SetActive(false);

        SoundManager.Instance.PauseUnPauseAudio(SoundManager.Instance.levelMusicSource);
        SoundManager.Instance.PauseUnPauseAudio(SoundManager.Instance.ambienceSource);
        Time.timeScale = 0f; // Pause the game

        StartCoroutine(FadeFirstButtonIn(ui, scrollingList, content));

        yield return new WaitForSecondsRealtime(3f); // Wait for 2 seconds in real time

        Time.timeScale = 1f; // Resume the game
        SoundManager.Instance.PauseUnPauseAudio(SoundManager.Instance.levelMusicSource);
        SoundManager.Instance.PauseUnPauseAudio(SoundManager.Instance.ambienceSource);
        playerHud.SetActive(true);
        childOne.SetActive(false);
        ui.SetActive(false); 
        
    }

    private IEnumerator FadeFirstButtonIn(GameObject ui, MonoBehaviour scrollingList, GameObject content)
    {
        Debug.Log("Starting FadeFirstButtonIn coroutine");

        var firstButton = content.transform.GetChild(0).GetComponent<Button>();
        var buttonText = firstButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();

        Color originalColor = buttonText.color;
        buttonText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        Color originalButtonColor = firstButton.GetComponent<Image>().color;
        firstButton.GetComponent<Image>().color = new Color(originalButtonColor.r, originalButtonColor.g, 
        originalButtonColor.b, 0f);

        float fadeDuration = 1f;
        float elapsedTime = 0f;

        SoundManager.Instance.sfxSource.PlayOneShot(entryFoundClip);


        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            buttonText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            firstButton.GetComponent<Image>().color = new Color(originalButtonColor.r, originalButtonColor.g, originalButtonColor.b, alpha);
            yield return null;
        }

    }
}
