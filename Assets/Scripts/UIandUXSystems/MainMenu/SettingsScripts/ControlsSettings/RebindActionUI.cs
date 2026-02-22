
/*
    Script provided by Unity that handles the core functionality of the rebinding settings. This script takes in the input asset assigned,
    and shows the respective name and button bind. By clicking on the button this script is attached to, it will allow the player to select
    a new key binding.
*/

using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;



namespace UnityEngine.InputSystem.Samples.RebindUI
{
    /// <summary>
    /// Inspector button to reset binding to default.
    /// </summary>
    public class RebindActionUI : MonoBehaviour
    {
        /// <summary>
        /// Reference to the action that is to be rebound.
        /// </summary>
        public InputActionReference actionReference
        {
            get => m_Action;
            set
            {
                m_Action = value;
                UpdateActionLabel();
                UpdateBindingDisplay();
            }
        }

        [ContextMenu("Reset Binding To Default")]
        public void InspectorResetToDefault()
        {
            ResetToDefault();
        }

        /// <summary>
        /// Returns the runtime InputAction instance from PlayerInput.actions matching m_Action.action.
        /// </summary>
        private InputAction GetRuntimeAction()
        {
            if (m_Action == null || m_Action.action == null)
                return null;
            var assetAction = m_Action.action;
            // Always resolve Pause from PlayerInput.actions for runtime rebinding
            var playerInput = InputReader.PlayerInput;
            if (playerInput != null && playerInput.actions != null)
            {
                if (assetAction.name == "Pause")
                {
                    // Always use runtime Pause action from PlayerInput.actions
                    foreach (var map in playerInput.actions.actionMaps)
                    {
                        var runtimePause = map.FindAction("Pause");
                        if (runtimePause != null)
                            return runtimePause;
                    }
                }
                var mapName = assetAction.actionMap != null ? assetAction.actionMap.name : string.Empty;
                if (!string.IsNullOrEmpty(mapName))
                {
                    var map = playerInput.actions.FindActionMap(mapName);
                    if (map != null)
                    {
                        var runtimeAction = map.FindAction(assetAction.name);
                        if (runtimeAction != null)
                            return runtimeAction;
                    }
                }
                return playerInput.actions.FindAction(assetAction.name);
            }
            return assetAction;
        }

        /// <summary>
        /// ID (in string form) of the binding that is to be rebound on the action.
        /// </summary>
        /// <seealso cref="InputBinding.id"/>
        public string bindingId
        {
            get => m_BindingId;
            set
            {
                m_BindingId = value;
                UpdateBindingDisplay();
            }
        }

        public InputBinding.DisplayStringOptions displayStringOptions
        {
            get => m_DisplayStringOptions;
            set
            {
                m_DisplayStringOptions = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Text component that receives the name of the action. Optional.
        /// </summary>
        public TMPro.TextMeshProUGUI actionLabel
        {
            get => m_ActionLabel;
            set
            {
                m_ActionLabel = value;
                UpdateActionLabel();
            }
        }

        /// <summary>
        /// Text component that receives the display string of the binding. Can be <c>null</c> in which
        /// case the component entirely relies on <see cref="updateBindingUIEvent"/>.
        /// </summary>
        public TMPro.TextMeshProUGUI bindingText
        {
            get => m_BindingText;
            set
            {
                m_BindingText = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Optional text component that receives a text prompt when waiting for a control to be actuated.
        /// </summary>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindOverlay"/>
        public TMPro.TextMeshProUGUI rebindPrompt
        {
            get => m_RebindText;
            set => m_RebindText = value;
        }

        /// <summary>
        /// Optional UI that is activated when an interactive rebind is started and deactivated when the rebind
        /// is finished. This is normally used to display an overlay over the current UI while the system is
        /// waiting for a control to be actuated.
        /// </summary>
        /// <remarks>
        /// If neither <see cref="rebindPrompt"/> nor <c>rebindOverlay</c> is set, the component will temporarily
        /// replaced the <see cref="bindingText"/> (if not <c>null</c>) with <c>"Waiting..."</c>.
        /// </remarks>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindPrompt"/>
        public GameObject rebindOverlay
        {
            get => m_RebindOverlay;
            set => m_RebindOverlay = value;
        }

        /// <summary>
        /// Event that is triggered every time the UI updates to reflect the current binding.
        /// This can be used to tie custom visualizations to bindings.
        /// </summary>
        public UpdateBindingUIEvent updateBindingUIEvent
        {
            get
            {
                if (m_UpdateBindingUIEvent == null)
                    m_UpdateBindingUIEvent = new UpdateBindingUIEvent();
                return m_UpdateBindingUIEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind is started on the action.
        /// </summary>
        public InteractiveRebindEvent startRebindEvent
        {
            get
            {
                if (m_RebindStartEvent == null)
                    m_RebindStartEvent = new InteractiveRebindEvent();
                return m_RebindStartEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind has been completed or canceled.
        /// </summary>
        public InteractiveRebindEvent stopRebindEvent
        {
            get
            {
                if (m_RebindStopEvent == null)
                    m_RebindStopEvent = new InteractiveRebindEvent();
                return m_RebindStopEvent;
            }
        }

        /// <summary>
        /// When an interactive rebind is in progress, this is the rebind operation controller.
        /// Otherwise, it is <c>null</c>.
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation ongoingRebind => m_RebindOperation;

        /// <summary>
        /// Return the action and binding index for the binding that is targeted by the component
        /// according to
        /// </summary>
        /// <param name="action"></param>
        /// <param name="bindingIndex"></param>
        /// <returns></returns>
        public bool ResolveActionAndBinding(out InputAction action, out int bindingIndex)
        {
            bindingIndex = -1;
            action = GetRuntimeAction();
            if (action == null)
                return false;
            if (string.IsNullOrEmpty(m_BindingId))
                return false;
            // Look up binding index.
            var bindingId = new Guid(m_BindingId);
            bindingIndex = action.bindings.IndexOf(x => x.id == bindingId);
            if (bindingIndex == -1)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Trigger a refresh of the currently displayed binding.
        /// </summary>
        public void UpdateBindingDisplay()
        {
            var displayString = string.Empty;
            var deviceLayoutName = default(string);
            var controlPath = default(string);

            // Check if binding text override is enabled
            if (overrideBindingText)
            {
                displayString = bindingTextString;
            }
            else
            {
                // Get display string from runtime action.
                var action = GetRuntimeAction();
                if (action != null)
                {
                    var bindingIndex = action.bindings.IndexOf(x => x.id.ToString() == m_BindingId);
                    if (bindingIndex != -1)
                    {
                        displayString = action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath, displayStringOptions);
                    }
                }
            }

            // Set on label (if any).
            if (m_BindingText != null)
                m_BindingText.text = displayString;

            // Give listeners a chance to configure UI in response.
            m_UpdateBindingUIEvent?.Invoke(this, displayString, deviceLayoutName, controlPath);
        }

        /// <summary>
        /// Remove currently applied binding overrides.
        /// </summary>
       

        public void ResetToDefault()
        {
            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
            {
                return;
            }

            ResetBinding(action, bindingIndex);

            if (action.bindings[bindingIndex].isComposite)
            {
                // It's a composite. Remove overrides from part bindings.
                for (var i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
                    action.RemoveBindingOverride(i);
            }
            else
            {
                action.RemoveBindingOverride(bindingIndex);
            }

            UpdateBindingDisplay();
        }

        private void ResetBinding(InputAction action, int bindingIndex)
        {
            InputBinding newBinding = action.bindings[bindingIndex];
            string oldOverridePath = newBinding.overridePath;

            action.RemoveBindingOverride(bindingIndex);
            int currentIndex = -1;

            foreach (InputAction otherAction in action.actionMap.actions)
            {
                currentIndex++;
                InputBinding currentBinding = action.actionMap.bindings[currentIndex];

                if (otherAction == action)
                {
                    if (newBinding.isPartOfComposite)
                    {
                        if (currentBinding.overridePath == newBinding.path)
                        {
                            otherAction.ApplyBindingOverride(currentIndex, oldOverridePath);
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                for (int i = 0; i < otherAction.bindings.Count; i++)
                {
                    InputBinding binding = otherAction.bindings[i];
                    if (binding.overridePath == newBinding.path)
                    {
                        otherAction.ApplyBindingOverride(i, oldOverridePath);
                    }
                }
            }
        }

        /// <summary>
        /// Initiate an interactive rebind that lets the player actuate a control to choose a new binding
        /// for the action.
        /// </summary>
        public void StartInteractiveRebind()
        {
            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
            {
                return;
            }

            // If the binding is a composite, we need to rebind each part in turn.
            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                    PerformInteractiveRebind(action, firstPartIndex, allCompositeParts: true);
            }
            else
            {
                PerformInteractiveRebind(action, bindingIndex);
            }
        }

        private void PerformInteractiveRebind(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            m_RebindOperation?.Cancel(); // Will null out m_RebindOperation.

            var binding = action.bindings[bindingIndex];

            void CleanUp()
            {
                m_RebindOperation?.Dispose();
                m_RebindOperation = null;
                action.actionMap.Enable();
                m_UIInputActionMap?.Enable();
            }

            //disable the action before use
            action.Disable();
            action.actionMap.Disable();
            m_UIInputActionMap?.Disable();

            // Configure the rebind.
            m_RebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(
                    operation =>
                    {
                        action.Enable();
                        m_RebindStopEvent?.Invoke(this, operation);
                        if (m_RebindOverlay != null)
                            m_RebindOverlay.SetActive(false);
                        UpdateBindingDisplay();
                        CleanUp();
                    })
                .OnComplete(
                    operation =>
                    {
                        // Hide rebind overlay
                        if (m_RebindOverlay != null)
                            m_RebindOverlay.SetActive(false);
                        
                        m_RebindStopEvent?.Invoke(this, operation);

                        if (CheckDuplicateBindings(action, bindingIndex, allCompositeParts))
                        {
                            ClearDuplicateBinding(action, bindingIndex);
                        }


                        // Update display to show new binding
                        UpdateBindingDisplay();

                        // Save rebinds immediately after a successful rebind
                        var rebindSaveLoad = UnityEngine.Object.FindFirstObjectByType<RebindSaveLoad>();
                        if (rebindSaveLoad != null)
                        {
                            rebindSaveLoad.SaveRebindsManually();
                        }

                        // Re-enable action and action maps
                        action.Enable();
                        CleanUp();

                        // If there's more composite parts we should bind, initiate a rebind
                        // for the next part.
                        if (allCompositeParts)
                        {
                            var nextBindingIndex = bindingIndex + 1;
                            if (nextBindingIndex < action.bindings.Count && action.bindings[nextBindingIndex].isPartOfComposite)
                            {
                                PerformInteractiveRebind(action, nextBindingIndex, true);
                            }
                        }
                    });

            // If it's a part binding, show the name of the part in the UI.
            var partName = default(string);
            if (action.bindings[bindingIndex].isPartOfComposite)
                partName = $"Binding '{action.bindings[bindingIndex].name}'. ";

            // Bring up rebind overlay, if we have one.
            m_RebindOverlay?.SetActive(true);
            if (m_RebindText != null)
            {
                var text = !string.IsNullOrEmpty(m_RebindOperation.expectedControlType)
                    ? $"{partName}Waiting for {m_RebindOperation.expectedControlType} input..."
                    : $"{partName}Waiting for input...";
                m_RebindText.text = text;
            }

            // If we have no rebind overlay and no callback but we have a binding text label,
            // temporarily set the binding text label to "<Waiting>".
            if (m_RebindOverlay == null && m_RebindText == null && m_RebindStartEvent == null && m_BindingText != null)
                m_BindingText.text = "<Waiting...>";

            // Give listeners a chance to act on the rebind starting.
            m_RebindStartEvent?.Invoke(this, m_RebindOperation);

            m_RebindOperation.Start();
        }

        private bool CheckDuplicateBindings(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            InputBinding newBinding = action.bindings[bindingIndex];
            string newPath = newBinding.effectivePath;
            

            // Check all bindings in the same action map for duplicates
            for (int i = 0; i < action.bindings.Count; i++)
            {
                // Skip checking against itself
                if (i == bindingIndex)
                    continue;

                InputBinding otherBinding = action.bindings[i];
                string otherPath = otherBinding.effectivePath;

                // Skip empty paths
                if (string.IsNullOrEmpty(newPath) || string.IsNullOrEmpty(otherPath))
                    continue;

                if (otherPath == newPath)
                {
                    return true;
                }
            }

            // Also check bindings in other actions within the same action map
            if (action.actionMap != null)
            {
                int actionMapBindingIndex = -1;
                for (int i = 0; i < action.actionMap.bindings.Count; i++)
                {
                    if (action.actionMap.bindings[i].id == newBinding.id)
                    {
                        actionMapBindingIndex = i;
                        break;
                    }
                }

                for (int i = 0; i < action.actionMap.bindings.Count; i++)
                {
                    // Skip current binding
                    if (i == actionMapBindingIndex)
                        continue;

                    InputBinding otherBinding = action.actionMap.bindings[i];
                    string otherPath = otherBinding.effectivePath;

                    // Skip empty paths
                    if (string.IsNullOrEmpty(newPath) || string.IsNullOrEmpty(otherPath))
                        continue;

                    if (otherPath == newPath)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ClearDuplicateBinding(InputAction action, int bindingIndex)
        {
            InputBinding newBinding = action.bindings[bindingIndex];
            string newPath = newBinding.effectivePath;

            if (string.IsNullOrEmpty(newPath))
                return;

            // Find and clear bindings in other actions that use the same path
            if (action.actionMap != null)
            {
                for (int i = 0; i < action.actionMap.bindings.Count; i++)
                {
                    InputBinding otherBinding = action.actionMap.bindings[i];
                    
                    // Skip the binding we just changed
                    if (otherBinding.id == newBinding.id)
                        continue;

                    string otherPath = otherBinding.effectivePath;
                    
                    if (otherPath == newPath)
                    {
                        // Find the action that owns this binding
                        var otherAction = action.actionMap.FindAction(otherBinding.action);
                        if (otherAction != null)
                        {
                            // Find the binding index in that action
                            int otherBindingIndex = otherAction.bindings.IndexOf(x => x.id == otherBinding.id);
                            if (otherBindingIndex >= 0)
                            {
                                // Apply an empty binding override to show "-" in the UI
                                otherAction.ApplyBindingOverride(otherBindingIndex, "");
                                
                                // Force update all RebindActionUI components that display this action
                                UpdateRebindUIForAction(otherAction);
                            }
                        }
                    }
                }
            }
        }
        private void UpdateRebindUIForAction(InputAction targetAction)
        {
            if (s_RebindActionUIs == null)
                return;

            for (int i = 0; i < s_RebindActionUIs.Count; i++)
            {
                var ui = s_RebindActionUIs[i];
                var referencedAction = ui.actionReference?.action;
                
                if (referencedAction == targetAction)
                {
                    // Disable and re-enable the action to force binding re-evaluation
                    bool wasEnabled = referencedAction.enabled;
                    referencedAction.Disable();
                    
                    ui.UpdateBindingDisplay();
                    
                    if (wasEnabled)
                        referencedAction.Enable();
                }
            }
        }

        protected void OnEnable()
        {
            if (s_RebindActionUIs == null)
                s_RebindActionUIs = new List<RebindActionUI>();
            s_RebindActionUIs.Add(this);
            if (s_RebindActionUIs.Count == 1)
                InputSystem.onActionChange += OnActionChange;
            if (m_DefaultInputActions != null && m_UIInputActionMap == null)
                m_UIInputActionMap = m_DefaultInputActions.FindActionMap("UI");
            // Refresh the binding display when component is enabled
            // This ensures saved bindings show the correct text when the scene loads
            UpdateActionLabel();
            UpdateBindingDisplay();
            // Hook up button click listeners
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(StartInteractiveRebind);
            }

        }

        protected void OnDisable()
        {
            m_RebindOperation?.Dispose();
            m_RebindOperation = null;

            // Remove button click listener
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(StartInteractiveRebind);
            }

            s_RebindActionUIs.Remove(this);
            if (s_RebindActionUIs.Count == 0)
            {
                s_RebindActionUIs = null;
                InputSystem.onActionChange -= OnActionChange;
            }
        }

        // When the action system re-resolves bindings, we want to update our UI in response. While this will
        // also trigger from changes we made ourselves, it ensures that we react to changes made elsewhere. If
        // the user changes keyboard layout, for example, we will get a BoundControlsChanged notification and
        // will update our UI to reflect the current keyboard layout.
        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged)
                return;

            var action = obj as InputAction;
            var actionMap = action?.actionMap ?? obj as InputActionMap;
            var actionAsset = actionMap?.asset ?? obj as InputActionAsset;

            for (var i = 0; i < s_RebindActionUIs.Count; ++i)
            {
                var component = s_RebindActionUIs[i];
                var referencedAction = component.actionReference?.action;
                if (referencedAction == null)
                    continue;

                if (referencedAction == action ||
                    referencedAction.actionMap == actionMap ||
                    referencedAction.actionMap?.asset == actionAsset)
                    component.UpdateBindingDisplay();
            }
        }

        [Tooltip("Reference to action that is to be rebound from the UI.")]
        [SerializeField]
        private InputActionReference m_Action;

        [SerializeField]
        private string m_BindingId;

        [SerializeField]
        private InputBinding.DisplayStringOptions m_DisplayStringOptions;

        [Tooltip("Text label that will receive the name of the action. Optional. Set to None to have the "
            + "rebind UI not show a label for the action.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_ActionLabel;

        [Tooltip("Text label that will receive the current, formatted binding string.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_BindingText;

        [Tooltip("Image component that displays the binding icon (controlled by GamepadIconsExample or similar icon handlers).")]
        [SerializeField]
        public Image m_BindingImage = null;

        [Tooltip("Optional UI that will be shown while a rebind is in progress.")]
        [SerializeField]
        private GameObject m_RebindOverlay;

        [Tooltip("Optional text label that will be updated with prompt for user input.")]
        [SerializeField]
        private TMPro.TextMeshProUGUI m_RebindText;

        [Tooltip("Optional bool field which allows you to override the action label with custom text")]
        public bool m_OverrideActionLabel;

        [Tooltip("What text should be displayed for the action label?")]
        [SerializeField]
        private string m_ActionLabelString;
        /// <summary>
        /// Whether to override the binding text with a custom string.
        /// </summary>
        public bool overrideBindingText
        {
            get => m_OverrideBindingText;
            set
            {
                m_OverrideBindingText = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// The custom text to display for the binding when override is enabled.
        /// </summary>
        public string bindingTextString
        {
            get => m_BindingTextString;
            set
            {
                m_BindingTextString = value;
                UpdateBindingDisplay();
            }
        }

        [Tooltip("Optional bool field which allows you to override the binding text with custom text")]
        [SerializeField]
        private bool m_OverrideBindingText;

        [Tooltip("What text should be displayed for the binding?")]
        [SerializeField]
        private string m_BindingTextString;
        

        [Tooltip("Optional reference to default input actions containing the UI action map. The UI action map is "
            + "disabled when rebinding is in progress.")]
        [SerializeField]
        private InputActionAsset m_DefaultInputActions;
        private InputActionMap m_UIInputActionMap;

        [Tooltip("Event that is triggered when the way the binding is display should be updated. This allows displaying "
            + "bindings in custom ways, e.g. using images instead of text.")]
        [SerializeField]
        private UpdateBindingUIEvent m_UpdateBindingUIEvent;

        [Tooltip("Event that is triggered when an interactive rebind is being initiated. This can be used, for example, "
            + "to implement custom UI behavior while a rebind is in progress. It can also be used to further "
            + "customize the rebind.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStartEvent;

        [Tooltip("Event that is triggered when an interactive rebind is complete or has been aborted.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStopEvent;

        private InputActionRebindingExtensions.RebindingOperation m_RebindOperation;

        private static List<RebindActionUI> s_RebindActionUIs;

        // We want the label for the action name to update in edit mode, too, so
        // we kick that off from here.
        #if UNITY_EDITOR
        protected void OnValidate()
        {
            UpdateActionLabel();
            UpdateBindingDisplay();
        }

        #endif

        private void UpdateActionLabel()
        {
            if (m_ActionLabel != null)
            {
                var action = GetRuntimeAction();
                if (m_OverrideActionLabel)
                {
                    m_ActionLabel.text = m_ActionLabelString;
                }
                else
                {
                    m_ActionLabel.text = action != null ? action.name : string.Empty;
                    m_ActionLabelString = String.Empty;
                }
            }
        }

        [Serializable]
        public class UpdateBindingUIEvent : UnityEvent<RebindActionUI, string, string, string>
        {
        }

        [Serializable]
        public class InteractiveRebindEvent : UnityEvent<RebindActionUI, InputActionRebindingExtensions.RebindingOperation>
        {
        }
    }
}
