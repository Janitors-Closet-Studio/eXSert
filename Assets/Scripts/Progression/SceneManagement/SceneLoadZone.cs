using UnityEngine;

namespace Progression.SceneManagement
{
    [RequireComponent(typeof(BoxCollider))]
    public class SceneLoadZone : ProgressionZone
    {
        [SerializeField, CriticalReference, Tooltip("The scene to load when the player enters this zone.")]
        private SceneAsset sceneToManage;
        [SerializeField, Tooltip("Should the scene be loaded or unloaded? Check to load scene")]
        private bool loadScene = true;

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
            Debug.Log($"Loading scene {sceneToManage.name} due to player entering zone {this.gameObject.name}.");
            sceneToManage.Load();
        }

        private void UnloadScene()
        {
            Debug.Log($"Unloading scene {sceneToManage.name} due to player entering zone {this.gameObject.name}.");
            sceneToManage.Unload();
        }
    }
}