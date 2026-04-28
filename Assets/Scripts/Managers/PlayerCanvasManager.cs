using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCanvasManager : MonoBehaviour
{
    [SerializeField] private SceneAsset sceneToHide;
    [SerializeField] private GameObject playerCanvas;
    [SerializeField, Tooltip("Optional explicit root Canvas object. If not assigned, it is resolved from this component hierarchy.")]
    private GameObject playerCanvasRoot;

    private GameObject ResolveCanvasRoot()
    {
        if (playerCanvasRoot != null)
            return playerCanvasRoot;

        Canvas canvasFromSelf = GetComponentInParent<Canvas>(true);
        if (canvasFromSelf != null)
        {
            playerCanvasRoot = canvasFromSelf.gameObject;
            return playerCanvasRoot;
        }

        if (playerCanvas != null)
        {
            Canvas canvasFromContent = playerCanvas.GetComponentInParent<Canvas>(true);
            if (canvasFromContent != null)
            {
                playerCanvasRoot = canvasFromContent.gameObject;
                return playerCanvasRoot;
            }
        }

        return null;
    }

    public void SetPlayerCanvasVisible(bool visible)
    {
        GameObject canvasRoot = ResolveCanvasRoot();
        if (canvasRoot != null && canvasRoot.activeSelf != visible)
            canvasRoot.SetActive(visible);

        if (playerCanvas != null && playerCanvas.activeSelf != visible)
            playerCanvas.SetActive(visible);

        if (visible && !gameObject.activeSelf)
            gameObject.SetActive(true);
    }


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
        if (sceneToHide == null)
        {
            SetPlayerCanvasVisible(true);
            return;
        }

        SetPlayerCanvasVisible(scene.name != sceneToHide.name);
    }
}
