/*
 * Author: Will Thomsen
 * 
 * A generic singleton class for Unity MonoBehaviours.
 * Ensures that only one instance of the class exists and provides a global access point to it.
 * The instance is created if it doesn't already exist and is marked to not be destroyed on scene load.
 */

using Unity.VisualScripting;
using UnityEngine;

// makes singletons a namespace that must be opted in to use
namespace Singletons {
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        // The private static instance of the singleton
        private static T _instance;

        /// <summary>
        /// Override to disable DontDestroyOnLoad behavior for Scene-scoped singletons.
        /// </summary>
        protected virtual bool ShouldPersistAcrossScenes => true;

        protected static bool isApplicationQuitting = false;

        /// <summary>
        /// Gets the singleton instance of type <typeparamref name="T"/>. If no instance exists in the scene, a new one
        /// is created automatically.
        /// </summary>
        /// <remarks>This property ensures that only one instance of the singleton type <typeparamref
        /// name="T"/> exists in the scene. If an instance is not found, a new <see cref="GameObject"/> is created and
        /// the singleton component is attached to it. If the singleton is configured to persist across scenes, the
        /// created object will not be destroyed on scene load.</remarks>
        public static T Instance
        {
            // special functionality which tries to find or creates the singleton instance if it doesn't exist already
            get
            {
                if (isApplicationQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed on application quit. Returning null.");
                    return null;
                }

                // Try to find an existing instance of the singleton type T in the scene
                if (_instance == null) _instance = (T)FindAnyObjectByType(typeof(T));

                // Return the found instance if it exists
                if (_instance != null) return _instance;

                // If no instance is found, create a new GameObject and attach the singleton component to it
                Debug.Log($"No instance of singleton {typeof(T)} found in the scene. Creating a new one.");
                _instance = CreateInstance();

                return _instance;
            }

            private set { _instance = value; }
        }

        protected static T CreateInstance()
        {
            if (isApplicationQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed on application quit. Returning null.");
                return null;
            }

            GameObject singletonObject = new();
            T newInstance = singletonObject.AddComponent<T>();
            (newInstance as Singleton<T>).UpdateInstanceName(); // Update the name of the GameObject to reflect the singleton type

            if (newInstance is Singleton<T> singleton && singleton.ShouldPersistAcrossScenes)
            {
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(singletonObject);
                }
            }

            return newInstance;
        }

        private void UpdateInstanceName() => name = $"{this} (Singleton)";
        public static void UpdateName() => (Instance as Singleton<T>).UpdateInstanceName();
        public override string ToString() => base.ToString();

        /// <summary>
        /// Initializes the singleton instance and ensures only one instance of the component exists in the scene.
        /// </summary>
        /// <remarks>If no instance exists, this method assigns the current component as the singleton
        /// instance. If <see cref="ShouldPersistAcrossScenes"/> is <see langword="true"/>, the component persists
        /// across scene loads. If another instance already exists, this method destroys the duplicate component and
        /// logs a warning.</remarks>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (ShouldPersistAcrossScenes && Application.isPlaying) 
                    DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"Another instance of singleton {typeof(T)} already exists. Destroying this component only.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            // If the application is quitting, we don't want to reset the instance reference
            if (isApplicationQuitting) return;
            // If this instance is being destroyed and it's the current singleton instance, reset the reference
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }
    }
}
