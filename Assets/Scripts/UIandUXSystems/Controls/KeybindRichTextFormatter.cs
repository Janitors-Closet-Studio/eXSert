using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public static class KeybindRichTextFormatter
{
    private static readonly Regex TokenRegex = new(
        @"\[\[(?<token>bind):(?<body>[^\]]*)\]\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static KeybindIconSet cachedIconSet;

    public static string Format(string source)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        return TokenRegex.Replace(source, ReplaceToken);
    }

    public static string ApplyDefaults(string source, KeybindAction? defaultAction, float defaultScale, string defaultColor)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        return TokenRegex.Replace(source, match => NormalizeToken(match, defaultAction, defaultScale, defaultColor));
    }

    private static string ReplaceToken(Match match)
    {
        string rawBody = match.Groups["body"].Value;
        string[] segments = rawBody.Split(',');
        if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0]))
            return match.Value;

        if (!Enum.TryParse(segments[0].Trim(), ignoreCase: true, out KeybindAction action))
            return match.Value;

        string partName = string.Empty;
        float scale = 1f;
        string color = string.Empty;

        for (int i = 1; i < segments.Length; i++)
        {
            string segment = segments[i].Trim();
            int equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= segment.Length - 1)
                continue;

            string key = segment.Substring(0, equalsIndex).Trim();
            string value = segment.Substring(equalsIndex + 1).Trim();

            switch (key.ToLowerInvariant())
            {
                case "part":
                    partName = value;
                    break;
                case "scale":
                case "size":
                    scale = ParseScale(value, scale);
                    break;
                case "color":
                    color = value;
                    break;
            }
        }

        KeybindIconSet iconSet = GetIconSet();
        if (iconSet == null)
            return match.Value;

        bool useGamepad = iconSet.IsGamepadScheme(GetCurrentScheme());
        bool foundIcon = string.IsNullOrEmpty(partName)
            ? iconSet.TryGetTmpIcon(action, useGamepad, out TMP_SpriteAsset spriteAsset, out string spriteName, out _)
            : iconSet.TryGetTmpCompositePartIcon(action, useGamepad, partName, out spriteAsset, out spriteName, out _);

        if (!foundIcon || spriteAsset == null || string.IsNullOrEmpty(spriteName))
            return match.Value;

        return BuildSpriteTag(spriteAsset, spriteName, scale, color);
    }

    private static string NormalizeToken(Match match, KeybindAction? defaultAction, float defaultScale, string defaultColor)
    {
        string rawBody = match.Groups["body"].Value;
        string[] segments = rawBody.Split(',');

        KeybindAction? action = null;
        string partName = string.Empty;
        string scale = string.Empty;
        string color = string.Empty;

        if (segments.Length > 0 && !string.IsNullOrWhiteSpace(segments[0]) &&
            Enum.TryParse(segments[0].Trim(), ignoreCase: true, out KeybindAction parsedAction))
        {
            action = parsedAction;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            string segment = segments[i].Trim();
            int equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= segment.Length - 1)
                continue;

            string key = segment.Substring(0, equalsIndex).Trim();
            string value = segment.Substring(equalsIndex + 1).Trim();

            switch (key.ToLowerInvariant())
            {
                case "part":
                    partName = value;
                    break;
                case "scale":
                case "size":
                    scale = value;
                    break;
                case "color":
                    color = value;
                    break;
            }
        }

        if (!action.HasValue)
            action = defaultAction;

        if (!action.HasValue)
            return match.Value;

        if (string.IsNullOrWhiteSpace(scale) && defaultScale > 0f && !Mathf.Approximately(defaultScale, 1f))
            scale = defaultScale.ToString("0.###", CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(color) && !string.IsNullOrWhiteSpace(defaultColor))
            color = defaultColor;

        StringBuilder builder = new();
        builder.Append("[[bind:").Append(action.Value);

        if (!string.IsNullOrWhiteSpace(partName))
            builder.Append(",part=").Append(partName);

        if (!string.IsNullOrWhiteSpace(scale))
            builder.Append(",size=").Append(scale);

        if (!string.IsNullOrWhiteSpace(color))
            builder.Append(",color=").Append(color);

        builder.Append("]]" );
        return builder.ToString();
    }

    private static string BuildSpriteTag(TMP_SpriteAsset spriteAsset, string spriteName, float scale, string color)
    {
        StringBuilder builder = new();
        bool hasColor = !string.IsNullOrWhiteSpace(color);
        if (hasColor)
            builder.Append("<color=").Append(color).Append('>');

        builder.Append("<sprite=")
            .Append('"').Append(spriteAsset.name).Append('"')
            .Append(" name=")
            .Append('"').Append(spriteName).Append('"')
            .Append(" tint=1");

        if (!Mathf.Approximately(scale, 1f))
            builder.Append(" scale=").Append(scale.ToString("0.###", CultureInfo.InvariantCulture));

        builder.Append('>');

        if (hasColor)
            builder.Append("</color>");

        return builder.ToString();
    }

    private static float ParseScale(string value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string trimmed = value.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            string percent = trimmed.Substring(0, trimmed.Length - 1);
            if (float.TryParse(percent, NumberStyles.Float, CultureInfo.InvariantCulture, out float percentValue))
                return percentValue / 100f;
        }

        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
            return scale;

        return fallback;
    }

    private static KeybindIconSet GetIconSet()
    {
        if (cachedIconSet == null)
            cachedIconSet = Resources.Load<KeybindIconSet>("KeybindIconSet");

        return cachedIconSet;
    }

    private static string GetCurrentScheme()
    {
        if (InputReader.Instance != null)
            return InputReader.activeControlScheme ?? string.Empty;

        if (InputReader.PlayerInput != null)
            return InputReader.PlayerInput.currentControlScheme ?? string.Empty;

        return string.Empty;
    }
}