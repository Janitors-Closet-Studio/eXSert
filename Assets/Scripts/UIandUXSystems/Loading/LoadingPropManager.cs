using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI.Loading
{
    /// <summary>
    /// Handles spawning and manipulating the showcase prop during the loading screen.
    /// </summary>
    public sealed class LoadingPropManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField]
        private List<LoadingPropDefinition> propDefinitions = new();

        [SerializeField]
        [Tooltip("UI Image that will display the selected loading prop sprite.")]
        private Image propImage;

        [SerializeField]
        [Tooltip("If enabled, the image will preserve the sprite aspect ratio.")]
        private bool preserveAspect = true;

        [SerializeField]
        private TMP_Text descriptionLabel;

        private void OnDisable()
        {
            ClearProp();
        }

        public void SetLookInput(Vector2 value)
        {
            // Loading showcase is sprite-based; look input is intentionally ignored.
        }

        public void SetZoomInput(float value)
        {
            // Loading showcase is sprite-based; zoom input is intentionally ignored.
        }

        public void ShowRandomProp()
        {
            if (propDefinitions == null || propDefinitions.Count == 0)
            {
                ClearProp();
                return;
            }

            int index = Random.Range(0, propDefinitions.Count);
            ShowProp(propDefinitions[index]);
        }

        public void ShowProp(LoadingPropDefinition definition)
        {
            ClearProp();
            if (definition == null)
            {
                if (descriptionLabel != null)
                    descriptionLabel.text = string.Empty;
                return;
            }

            if (propImage != null)
            {
                propImage.sprite = definition.propSprite;
                propImage.preserveAspect = preserveAspect;
                propImage.enabled = propImage.sprite != null;
            }

            if (descriptionLabel != null)
            {
                if (string.IsNullOrEmpty(definition.displayName))
                    descriptionLabel.text = $"<size=28>{definition.description}</size>";
                else
                    descriptionLabel.text =
                        $"<size=36><b>{definition.displayName}</b></size>\n\n<size=28>{definition.description}</size>";
            }
        }

        public void ClearProp()
        {
            if (propImage != null)
            {
                propImage.sprite = null;
                propImage.enabled = false;
            }

            if (descriptionLabel != null)
                descriptionLabel.text = string.Empty;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Adds inspector buttons so designers can spawn/clear props while testing in Play Mode.
    /// </summary>
    [CustomEditor(typeof(LoadingPropManager))]
    public sealed class LoadingPropManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Spawn Random Prop"))
                {
                    foreach (Object targetObj in targets)
                    {
                        if (targetObj is LoadingPropManager manager)
                            manager.ShowRandomProp();
                    }
                }

                if (GUILayout.Button("Clear Prop"))
                {
                    foreach (Object targetObj in targets)
                    {
                        if (targetObj is LoadingPropManager manager)
                            manager.ClearProp();
                    }
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to use the debug buttons.",
                    MessageType.Info
                );
            }
        }
    }
#endif
}
