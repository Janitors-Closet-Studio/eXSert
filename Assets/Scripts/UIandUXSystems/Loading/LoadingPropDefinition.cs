using UnityEngine;

namespace UI.Loading
{
    /// <summary>
    /// Data container that drives the prop showcase on the loading screen.
    /// </summary>
    [CreateAssetMenu(menuName = "Loading/Prop Definition", fileName = "NewLoadingProp")]
    public sealed class LoadingPropDefinition : ScriptableObject
    {
        [Header("Presentation")]
        [Tooltip("Optional friendly name shown above the description.")]
        public string displayName;

        [Tooltip("Lore or flavor text that will be displayed next to the prop render.")]
        [TextArea(2, 4)]
        public string description;

        [Header("Sprite")]
        [Tooltip(
            "Sprite that will be shown inside the loading scene showcase (1x1 square sprite recommended)."
        )]
        public Sprite propSprite;
    }
}
