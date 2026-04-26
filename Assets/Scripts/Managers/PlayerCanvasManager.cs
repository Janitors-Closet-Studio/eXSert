using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCanvasManager : MonoBehaviour
{
    [SerializeField] private SceneAsset sceneToHide;
    [SerializeField] private GameObject playerCanvas;


    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneToHide.name)
        {
            playerCanvas.SetActive(false);
        }
        else
            playerCanvas.SetActive(true);
        
    }
}
