using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "UI/Keybind Icon Set", fileName = "KeybindIconSet")]
public class KeybindIconSet : ScriptableObject
{
    [Serializable]
    public struct ControlIcon
    {
        public string controlPath;
        public Sprite icon;
    }

    [Serializable]
    public struct ActionBinding
    {
        public KeybindAction action;
        public InputActionReference actionReference;
        [Tooltip("Optional binding ID for keyboard/mouse. If empty, the first binding in the group is used.")]
        public string keyboardBindingId;
        [Tooltip("Optional binding ID for gamepad. If empty, the first binding in the group is used.")]
        public string gamepadBindingId;
        [Tooltip("Optional binding group override for keyboard/mouse (default uses Keyboard&Mouse).")]
        public string keyboardBindingGroup;
        [Tooltip("Optional binding group override for gamepad (default uses Gamepad).")]
        public string gamepadBindingGroup;
    }

    [Header("Control Scheme Names")]
    [SerializeField] private string keyboardMouseSchemeName = "Keyboard&Mouse";
    [SerializeField] private string gamepadSchemeName = "Gamepad";

    [Header("Action Bindings")]
    [SerializeField] private List<ActionBinding> actionBindings = new List<ActionBinding>();

    [Header("Keyboard/Mouse Icons")]
    [SerializeField] private List<ControlIcon> keyboardIcons = new List<ControlIcon>();

    [Header("Gamepad Icons")]
    [SerializeField] private List<ControlIcon> gamepadIcons = new List<ControlIcon>();

    [Header("Fallback Icons")]
    [SerializeField] private Sprite keyboardFallbackIcon;
    [SerializeField] private Sprite gamepadFallbackIcon;

    [Header("Editor Helpers")]
    [SerializeField] private bool autoSyncIconLists = true;
    [SerializeField] private bool autoPopulateIconLibrary = true;
    [SerializeField] private string keyboardIconsFolder = "Assets/eXSert Assets/2D Assets/UI/Icons/KeyboardIcons";
    [SerializeField] private string gamepadIconsFolder = "Assets/eXSert Assets/2D Assets/UI/Icons/ControllerIcons";

    public string KeyboardMouseSchemeName => keyboardMouseSchemeName;
    public string GamepadSchemeName => gamepadSchemeName;

    
    public bool TryGetIcon(KeybindAction actionId, bool useGamepad, out Sprite icon, out string controlPath)
    {
        icon = null;
        controlPath = string.Empty;

        if (!TryGetBindingPath(actionId, useGamepad, out controlPath))
        {
            icon = useGamepad ? gamepadFallbackIcon : keyboardFallbackIcon;
            return icon != null;
        }

        icon = GetIconForControlPath(controlPath, useGamepad);
        if (icon != null)
            return true;

        icon = useGamepad ? gamepadFallbackIcon : keyboardFallbackIcon;
        return icon != null;
    }

    public bool TryGetCompositePartIcon(
        KeybindAction actionId,
        bool useGamepad,
        string partName,
        out Sprite icon,
        out string controlPath)
    {
        icon = null;
        controlPath = string.Empty;

        if (!TryGetCompositePartPath(actionId, useGamepad, partName, out controlPath))
        {
            icon = useGamepad ? gamepadFallbackIcon : keyboardFallbackIcon;
            return icon != null;
        }

        icon = GetIconForControlPath(controlPath, useGamepad);
        if (icon != null)
            return true;

        icon = useGamepad ? gamepadFallbackIcon : keyboardFallbackIcon;
        return icon != null;
    }

    public bool TryGetBindingPath(KeybindAction actionId, bool useGamepad, out string controlPath)
    {
        controlPath = string.Empty;

        if (!TryGetActionBinding(actionId, out ActionBinding bindingData))
        {
            return false;
        }

        if (bindingData.actionReference == null || bindingData.actionReference.action == null)
        {
            return false;
        }

        var assetAction = bindingData.actionReference.action;
        InputAction runtimeAction = ResolveRuntimeAction(assetAction);
        if (runtimeAction == null)
        {
            Debug.Log($"[KeybindIconSet] Could not resolve runtime action for {assetAction?.name}");
            if (InputReader.PlayerInput != null && InputReader.PlayerInput.actions != null)
            {
                Debug.Log("[KeybindIconSet] Available action maps:");
                foreach (var map in InputReader.PlayerInput.actions.actionMaps)
                {
                    Debug.Log($"  Map: {map.name}");
                    foreach (var act in map.actions)
                    {
                        Debug.Log($"    Action: {act.name}");
                    }
                }
            }
            return false;
        }
        var action = runtimeAction;

        // Force refresh of bindings after a rebind
        if (!action.enabled)
        {
            action.Enable();
        }

        // Dump all bindings for debug
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
        }

        int bindingIndex = ResolveBindingIndex(action, bindingData, useGamepad);
        if (bindingIndex >= 0 && bindingIndex < action.bindings.Count)
        {
            var binding = action.bindings[bindingIndex];
            // Always use effectivePath after rebinding
            controlPath = string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;
            if (!string.IsNullOrEmpty(controlPath))
                return true;
        }

        bool compositeResult = TryGetCompositeControlPath(action, bindingData, useGamepad, out controlPath);
        return compositeResult;
    }

    public bool TryGetCompositePartPath(
        KeybindAction actionId,
        bool useGamepad,
        string partName,
        out string controlPath)
    {
        controlPath = string.Empty;
        if (string.IsNullOrEmpty(partName))
            return false;

        if (!TryGetActionBinding(actionId, out ActionBinding bindingData))
            return false;

        if (bindingData.actionReference == null || bindingData.actionReference.action == null)
            return false;

            var assetAction = bindingData.actionReference.action;
            InputAction runtimeAction = ResolveRuntimeAction(assetAction);
            if (runtimeAction == null)
                return false;
            var action = runtimeAction;
        string groupName = useGamepad ? bindingData.gamepadBindingGroup : bindingData.keyboardBindingGroup;
        if (string.IsNullOrEmpty(groupName))
            groupName = useGamepad ? gamepadSchemeName : keyboardMouseSchemeName;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.isComposite)
                continue;

            bool groupMatch = HasBindingGroup(binding, groupName);
            if (!groupMatch && !CompositeHasSchemeParts(action, i, useGamepad))
                continue;

            if (TryGetCompositePartPath(action, i, useGamepad, partName, groupMatch, out controlPath))
                return true;
        }

        return false;
    }

    public bool IsGamepadScheme(string schemeName)
    {
        if (string.IsNullOrEmpty(schemeName))
            return false;

        return string.Equals(schemeName, gamepadSchemeName, StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (actionBindings != null)
        {
            for (int i = 0; i < actionBindings.Count; i++)
            {
                ActionBinding binding = actionBindings[i];
                bool changed = false;

                if (string.IsNullOrEmpty(binding.keyboardBindingGroup))
                {
                    binding.keyboardBindingGroup = keyboardMouseSchemeName;
                    changed = true;
                }

                if (string.IsNullOrEmpty(binding.gamepadBindingGroup))
                {
                    binding.gamepadBindingGroup = gamepadSchemeName;
                    changed = true;
                }

                if (changed)
                    actionBindings[i] = binding;
            }
        }

        if (autoPopulateIconLibrary)
        {
            keyboardIcons = BuildKeyboardIconLibrary();
            gamepadIcons = BuildGamepadIconLibrary();
        }
        else if (autoSyncIconLists)
        {
            keyboardIcons = SyncIconListFromBindings(keyboardIcons, useGamepad: false);
            gamepadIcons = SyncIconListFromBindings(gamepadIcons, useGamepad: true);
        }
    }
#endif

    private bool TryGetActionBinding(KeybindAction actionId, out ActionBinding bindingData)
    {
        for (int i = 0; i < actionBindings.Count; i++)
        {
            if (actionBindings[i].action == actionId)
            {
                bindingData = actionBindings[i];
                return true;
            }
        }

        bindingData = default;
        return false;
    }

    private int ResolveBindingIndex(InputAction action, ActionBinding bindingData, bool useGamepad)
    {
        string bindingId = useGamepad ? bindingData.gamepadBindingId : bindingData.keyboardBindingId;
        if (!string.IsNullOrEmpty(bindingId))
        {
            if (Guid.TryParse(bindingId, out Guid id))
                return action.bindings.IndexOf(x => x.id == id);
        }

        string groupName = useGamepad ? bindingData.gamepadBindingGroup : bindingData.keyboardBindingGroup;
        if (string.IsNullOrEmpty(groupName))
            groupName = useGamepad ? gamepadSchemeName : keyboardMouseSchemeName;

        int fallbackIndex = -1;
        int deviceMatchIndex = -1;
        int groupMatchIndex = -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            if (fallbackIndex < 0)
                fallbackIndex = i;

            if (deviceMatchIndex < 0 && IsBindingForScheme(binding, useGamepad))
                deviceMatchIndex = i;

            if (string.IsNullOrEmpty(binding.groups))
                continue;

            if (binding.groups.Contains(groupName))
                groupMatchIndex = i;
        }

        if (groupMatchIndex >= 0)
            return groupMatchIndex;

        if (deviceMatchIndex >= 0)
            return deviceMatchIndex;

        return fallbackIndex;
    }

    private static InputAction ResolveRuntimeAction(InputAction action)
    {
        if (action == null)
            return null;

        if (InputReader.PlayerInput == null || InputReader.PlayerInput.actions == null)
            return null;

        string mapName = action.actionMap != null ? action.actionMap.name : string.Empty;
        if (!string.IsNullOrEmpty(mapName))
        {
            var map = InputReader.PlayerInput.actions.FindActionMap(mapName);
            if (map != null)
            {
                var runtimeAction = map.FindAction(action.name);
                if (runtimeAction != null)
                    return runtimeAction;
            }
        }

        return InputReader.PlayerInput.actions.FindAction(action.name);
    }

    private bool TryGetCompositeControlPath(InputAction action, ActionBinding bindingData, bool useGamepad, out string controlPath)
    {
        controlPath = string.Empty;
        string groupName = useGamepad ? bindingData.gamepadBindingGroup : bindingData.keyboardBindingGroup;
        if (string.IsNullOrEmpty(groupName))
            groupName = useGamepad ? gamepadSchemeName : keyboardMouseSchemeName;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.isComposite)
                continue;

            bool groupMatch = HasBindingGroup(binding, groupName);
            if (!groupMatch && !CompositeHasSchemeParts(action, i, useGamepad))
                continue;

            if (TryGetCompositeRepresentativePath(action, i, useGamepad, out controlPath))
                return true;
        }

        return false;
    }

    private static bool TryGetCompositePartPath(
        InputAction action,
        int compositeIndex,
        bool useGamepad,
        string partName,
        bool groupMatch,
        out string controlPath)
    {
        controlPath = string.Empty;

        for (int i = compositeIndex + 1; i < action.bindings.Count; i++)
        {
            var part = action.bindings[i];
            if (!part.isPartOfComposite)
                break;

            if (!string.Equals(part.name, partName, StringComparison.OrdinalIgnoreCase))
                continue;

            string path = string.IsNullOrEmpty(part.effectivePath) ? part.path : part.effectivePath;
            if (string.IsNullOrEmpty(path))
                continue;

            if (groupMatch || IsPathForScheme(path, useGamepad))
            {
                controlPath = path;
                return true;
            }
        }

        return false;
    }

    private static bool CompositeHasSchemeParts(InputAction action, int compositeIndex, bool useGamepad)
    {
        for (int i = compositeIndex + 1; i < action.bindings.Count; i++)
        {
            var part = action.bindings[i];
            if (!part.isPartOfComposite)
                break;

            string path = string.IsNullOrEmpty(part.effectivePath) ? part.path : part.effectivePath;
            if (IsPathForScheme(path, useGamepad))
                return true;
        }

        return false;
    }

    private static bool TryGetCompositeRepresentativePath(InputAction action, int compositeIndex, bool useGamepad, out string controlPath)
    {
        controlPath = string.Empty;
        string firstPartPath = string.Empty;
        bool hasDpad = false;
        bool hasLeftStick = false;

        for (int i = compositeIndex + 1; i < action.bindings.Count; i++)
        {
            var part = action.bindings[i];
            if (!part.isPartOfComposite)
                break;

            string path = string.IsNullOrEmpty(part.effectivePath) ? part.path : part.effectivePath;
            if (string.IsNullOrEmpty(path))
                continue;

            if (string.IsNullOrEmpty(firstPartPath))
                firstPartPath = path;

            if (!useGamepad)
                continue;

            if (path.StartsWith("<Gamepad>/dpad", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<XInputController>/dpad", StringComparison.OrdinalIgnoreCase))
                hasDpad = true;

            if (path.StartsWith("<Gamepad>/leftStick", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<XInputController>/leftStick", StringComparison.OrdinalIgnoreCase))
                hasLeftStick = true;
        }

        if (useGamepad)
        {
            if (hasDpad)
                controlPath = "<Gamepad>/dpad";
            else if (hasLeftStick)
                controlPath = "<Gamepad>/leftStick";
            else
                controlPath = firstPartPath;
        }
        else
        {
            controlPath = firstPartPath;
        }

        return !string.IsNullOrEmpty(controlPath);
    }

    private Sprite GetIconForControlPath(string controlPath, bool useGamepad)
    {
        if (string.IsNullOrEmpty(controlPath))
            return null;

        var iconList = useGamepad ? gamepadIcons : keyboardIcons;
        string shortPath = ExtractControlName(controlPath);
        
        for (int i = 0; i < iconList.Count; i++)
        {
            if (iconList[i].icon == null)
                continue;

            if (PathsMatch(iconList[i].controlPath, controlPath, shortPath))
                return iconList[i].icon;
        }

        return null;
    }

    private static bool PathsMatch(string candidatePath, string fullPath, string shortPath)
    {
        if (string.IsNullOrEmpty(candidatePath))
            return false;

        if (string.Equals(candidatePath, fullPath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(shortPath) && string.Equals(candidatePath, shortPath, StringComparison.OrdinalIgnoreCase))
            return true;

        string candidateShort = ExtractControlName(candidatePath);
        return !string.IsNullOrEmpty(candidateShort)
            && string.Equals(candidateShort, shortPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractControlName(string controlPath)
    {
        if (string.IsNullOrEmpty(controlPath))
            return string.Empty;

        int slashIndex = controlPath.LastIndexOf('/');
        if (slashIndex < 0 || slashIndex == controlPath.Length - 1)
            return controlPath;

        return controlPath.Substring(slashIndex + 1);
    }

    private static bool IsBindingForScheme(InputBinding binding, bool useGamepad)
    {
        string path = string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;
        return IsPathForScheme(path, useGamepad);
    }

    private static bool IsPathForScheme(string path, bool useGamepad)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (useGamepad)
            return path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<XInputController>", StringComparison.OrdinalIgnoreCase);

        return path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasBindingGroup(InputBinding binding, string groupName)
    {
        if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(binding.groups))
            return false;

        return binding.groups.Contains(groupName);
    }

#if UNITY_EDITOR
    private List<ControlIcon> BuildKeyboardIconLibrary()
    {
        Dictionary<string, Sprite> spriteMap = LoadSpriteMap(keyboardIconsFolder);
        List<ControlIcon> icons = new List<ControlIcon>();
        Dictionary<string, Sprite> pathMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i <= 9; i++)
            AddIconBySpriteName(pathMap, spriteMap, $"KB_{i}", $"<Keyboard>/{i}");

        for (char c = 'A'; c <= 'Z'; c++)
            AddIconBySpriteName(pathMap, spriteMap, $"KB_{c}", $"<Keyboard>/{char.ToLowerInvariant(c)}");

        for (int i = 1; i <= 12; i++)
            AddIconBySpriteName(pathMap, spriteMap, $"KB_F{i}", $"<Keyboard>/f{i}");

        AddIconBySpriteName(pathMap, spriteMap, "KB_ALT", "<Keyboard>/leftAlt");
        AddIconBySpriteName(pathMap, spriteMap, "KB_ALT", "<Keyboard>/rightAlt");
        AddIconBySpriteName(pathMap, spriteMap, "KB_CTRL", "<Keyboard>/leftCtrl");
        AddIconBySpriteName(pathMap, spriteMap, "KB_CTRL", "<Keyboard>/rightCtrl");
        AddIconBySpriteName(pathMap, spriteMap, "KB_SHIFT", "<Keyboard>/leftShift");
        AddIconBySpriteName(pathMap, spriteMap, "KB_SHIFT", "<Keyboard>/rightShift");
        AddIconBySpriteName(pathMap, spriteMap, "KB_SHIFT", "<Keyboard>/shift");

        AddIconBySpriteName(pathMap, spriteMap, "KB_BACK", "<Keyboard>/backspace");
        AddIconBySpriteName(pathMap, spriteMap, "KB_CAPS", "<Keyboard>/capsLock");
        AddIconBySpriteName(pathMap, spriteMap, "KB_DEL", "<Keyboard>/delete");
        AddIconBySpriteName(pathMap, spriteMap, "KB_END", "<Keyboard>/end");
        AddIconBySpriteName(pathMap, spriteMap, "KB_ENTER", "<Keyboard>/enter");
        AddIconBySpriteName(pathMap, spriteMap, "KB_ESC", "<Keyboard>/escape");
        AddIconBySpriteName(pathMap, spriteMap, "KB_HOME", "<Keyboard>/home");
        AddIconBySpriteName(pathMap, spriteMap, "KB_INS", "<Keyboard>/insert");
        AddIconBySpriteName(pathMap, spriteMap, "KB_NUMLOCK", "<Keyboard>/numLock");
        AddIconBySpriteName(pathMap, spriteMap, "KB_PAGEDOWN", "<Keyboard>/pageDown");
        AddIconBySpriteName(pathMap, spriteMap, "KB_PAGEUP", "<Keyboard>/pageUp");
        AddIconBySpriteName(pathMap, spriteMap, "KB_SPACE", "<Keyboard>/space");
        AddIconBySpriteName(pathMap, spriteMap, "KB_TAB", "<Keyboard>/tab");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Tab", "<Keyboard>/tab");

        AddIconBySpriteName(pathMap, spriteMap, "KB_Comma", "<Keyboard>/comma");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Period", "<Keyboard>/period");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Colon", "<Keyboard>/semicolon");
        AddIconBySpriteName(pathMap, spriteMap, "KB_SemiColon", "<Keyboard>/semicolon");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Dash", "<Keyboard>/minus");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Equals", "<Keyboard>/equals");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Plus", "<Keyboard>/equals");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Tick", "<Keyboard>/backquote");
        AddIconBySpriteName(pathMap, spriteMap, "KB_Asterisk", "<Keyboard>/8");
        AddIconBySpriteName(pathMap, spriteMap, "KB_BackSlash", "<Keyboard>/backslash");
        AddIconBySpriteName(pathMap, spriteMap, "KB_FwdSlash", "<Keyboard>/slash");
        AddIconBySpriteName(pathMap, spriteMap, "KB_LeftBracket", "<Keyboard>/leftBracket");
        AddIconBySpriteName(pathMap, spriteMap, "KB_RightBracket", "<Keyboard>/rightBracket");
        AddIconBySpriteName(pathMap, spriteMap, "KB_LeftArrow", "<Keyboard>/leftArrow");
        AddIconBySpriteName(pathMap, spriteMap, "KB_RightArrow", "<Keyboard>/rightArrow");
        AddIconBySpriteName(pathMap, spriteMap, "KB_UpArrow", "<Keyboard>/upArrow");
        AddIconBySpriteName(pathMap, spriteMap, "KB_DownArrow", "<Keyboard>/downArrow");

        AddIconBySpriteName(pathMap, spriteMap, "KB_LeftMouseClick", "<Mouse>/leftButton");
        AddIconBySpriteName(pathMap, spriteMap, "KB_RightMouseClick", "<Mouse>/rightButton");
        AddIconBySpriteName(pathMap, spriteMap, "KB_MouseScroll", "<Mouse>/scroll");
        AddIconBySpriteName(pathMap, spriteMap, "KB_MouseDelta", "<Mouse>/delta");

        if (keyboardFallbackIcon == null && spriteMap.TryGetValue("KB_EMPTY", out Sprite emptyKey))
            keyboardFallbackIcon = emptyKey;

        foreach (var entry in pathMap)
            icons.Add(new ControlIcon { controlPath = entry.Key, icon = entry.Value });

        return icons;
    }

    private List<ControlIcon> BuildGamepadIconLibrary()
    {
        Dictionary<string, Sprite> spriteMap = LoadSpriteMap(gamepadIconsFolder);
        List<ControlIcon> icons = new List<ControlIcon>();
        Dictionary<string, Sprite> pathMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        AddIconBySpriteName(pathMap, spriteMap, "Cont_A", "<Gamepad>/buttonSouth");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_B", "<Gamepad>/buttonEast");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_X", "<Gamepad>/buttonWest");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_Y", "<Gamepad>/buttonNorth");

        AddIconBySpriteName(pathMap, spriteMap, "Cont_LB", "<Gamepad>/leftShoulder");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_RB", "<Gamepad>/rightShoulder");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_LT", "<Gamepad>/leftTrigger");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_RT", "<Gamepad>/rightTrigger");

        AddIconBySpriteName(pathMap, spriteMap, "Cont_LPress", "<Gamepad>/leftStickPress");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_RPress", "<Gamepad>/rightStickPress");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_LStick", "<Gamepad>/leftStick");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_RStick", "<Gamepad>/rightStick");

        AddIconBySpriteName(pathMap, spriteMap, "Cont_DpadUp", "<Gamepad>/dpad/up");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_DpadDown", "<Gamepad>/dpad/down");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_DpadLeft", "<Gamepad>/dpad/left");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_DpadRight", "<Gamepad>/dpad/right");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_Dpad", "<Gamepad>/dpad");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_LeftArrow", "<Gamepad>/dpad/left");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_RightArrow", "<Gamepad>/dpad/right");

        AddIconBySpriteName(pathMap, spriteMap, "Cont_Setting", "<Gamepad>/start");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_Share", "<Gamepad>/select");
        AddIconBySpriteName(pathMap, spriteMap, "Cont_Setting", "<Gamepad>/select");

        if (gamepadFallbackIcon == null && spriteMap.TryGetValue("Cont_Controller", out Sprite controllerIcon))
            gamepadFallbackIcon = controllerIcon;

        foreach (var entry in pathMap)
            icons.Add(new ControlIcon { controlPath = entry.Key, icon = entry.Value });

        return icons;
    }

    private static void AddIconBySpriteName(
        Dictionary<string, Sprite> pathMap,
        Dictionary<string, Sprite> spriteMap,
        string spriteName,
        string controlPath)
    {
        if (pathMap.ContainsKey(controlPath))
            return;

        if (spriteMap.TryGetValue(spriteName, out Sprite sprite) && sprite != null)
            pathMap.Add(controlPath, sprite);
    }

    private static Dictionary<string, Sprite> LoadSpriteMap(string folderPath)
    {
        Dictionary<string, Sprite> map = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(folderPath))
            return map;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        HashSet<string> paths = new HashSet<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        foreach (string path in paths)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && !map.ContainsKey(sprite.name))
                    map.Add(sprite.name, sprite);
            }
        }

        return map;
    }

    private List<ControlIcon> SyncIconListFromBindings(List<ControlIcon> existing, bool useGamepad)
    {
        Dictionary<string, Sprite> existingMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        if (existing != null)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                if (string.IsNullOrEmpty(existing[i].controlPath))
                    continue;

                if (!existingMap.ContainsKey(existing[i].controlPath))
                    existingMap.Add(existing[i].controlPath, existing[i].icon);
            }
        }

        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < actionBindings.Count; i++)
        {
            var bindingData = actionBindings[i];
            if (bindingData.actionReference == null || bindingData.actionReference.action == null)
                continue;

            var action = bindingData.actionReference.action;
            int bindingIndex = ResolveBindingIndex(action, bindingData, useGamepad);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                continue;

            var binding = action.bindings[bindingIndex];
            string controlPath = string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;
            if (string.IsNullOrEmpty(controlPath))
                continue;

            paths.Add(controlPath);
        }

        List<ControlIcon> updated = new List<ControlIcon>();
        foreach (string path in paths)
        {
            ControlIcon icon = new ControlIcon
            {
                controlPath = path,
                icon = existingMap.TryGetValue(path, out Sprite sprite) ? sprite : null
            };
            updated.Add(icon);
        }

        return updated;
    }
#endif
}

public enum KeybindAction
{
    // NOTE: Explicit values preserve Unity serialization.
    GP_Dash = 0,
    GP_Guard = 1,
    GP_TargetLock = 2,
    GP_NavigationMenu = 3,
    GP_ChangeTarget = 4,
    GP_ChangeTarget_L = 16,
    GP_ChangeTarget_R = 17,
    GP_PauseMenu = 5,
    GP_HeavyAttackAoe = 6,
    GP_FastAttackSingle = 7,
    GP_Interact = 8,
    GP_Jump = 9,

    UI_Navigate = 11,
    UI_Swap = 18,
    UI_RestoreDefault = 19,
    UI_Confirm = 15,
    UI_Cancel = 16,

    // Crane actions moved to the bottom for inspector readability.
    CraneExit = 12,
    CraneConfirm = 13,
    CraneMove = 14
}
