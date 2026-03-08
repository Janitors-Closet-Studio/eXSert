using UnityEngine;

namespace Progression.SceneManagement
{
    [RequireComponent(typeof(BoxCollider))]
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.ifc87dwst3ky")]
    public class SceneLoadZone : ProgressionZone
    {
        [SerializeField, Tooltip("Should the scene be loaded or unloaded? Check to load scene")]
        private bool loadScene = true;
        [SerializeField, CriticalReference, Tooltip("The scene to load when the player enters this zone.")]
        private SceneAsset sceneToManage;

        [SerializeField, Tooltip("Optional object to enable after this zone unloads its target scene. Useful for turning on a blocker in the current scene.")]
        private GameObject enableObjectAfterUnload;

        protected override Color DebugColor => Color.yellow;

        private void OnValidate()
        {
            if (sceneToManage == null)
            {
                Debug.LogWarning($"SceneLoadZone on {this.gameObject.name} does not have a scene assigned to load.");
            }


            if (sceneToManage == SceneAsset.GetSceneAssetOfObject(this.gameObject))
            {
                Debug.LogError($"SceneLoadZone on {this.gameObject.name} is trying to load/unload the scene it is part of. This is not allowed and will cause issues.");
            }
        }

        protected override void PlayerEnteredZone()
        {
            if (loadScene) LoadScene();
            else UnloadScene();
        }

        protected override void PlayerExitedZone()
        {
            // We will load the scene immediately when the player enters the zone, so we don't need to do anything on exit.
        }

        private void LoadScene()
        {
            if (sceneToManage == null)
            {
                Debug.LogError($"SceneLoadZone on {gameObject.name} cannot load because no SceneAsset is assigned.");
                return;
            }

            Debug.Log($"Loading scene {sceneToManage.name} due to player entering zone {this.gameObject.name}.");
            SceneLoader.Load(sceneToManage, loadScreen: false);
        }

        private void UnloadScene()
        {
            if (sceneToManage == null)
            {
                Debug.LogError($"SceneLoadZone on {gameObject.name} cannot unload because no SceneAsset is assigned.");
                return;
            }

            Debug.Log($"Unloading scene {sceneToManage.name} due to player entering zone {this.gameObject.name}.");

            AsyncOperation unloadOperation = SceneLoader.Unload(sceneToManage);
            if (enableObjectAfterUnload == null)
                return;

            if (unloadOperation != null)
            {
                unloadOperation.completed += _ => EnableConfiguredObject();
            }
            else
            {
                EnableConfiguredObject();
            }
        }

        private void EnableConfiguredObject()
        {
            if (enableObjectAfterUnload == null)
                return;

            enableObjectAfterUnload.SetActive(true);
        }
    }
}