using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIandUXSystems.HUD
{
    public enum HUDMessageType
    {
        /// <summary>
        /// Represents a goal or target within the application.
        /// </summary>
        /// <remarks>
        /// Will remain visible until explicitly changed by a new objective.
        /// </remarks>
        Objective,

        /// <summary>
        /// Represents a notification or message intended to inform users of important events or information.
        /// </summary>
        /// <remarks>
        /// Will temporarily fade into view on the HUD and will shortly after fade out and disappear.
        /// </remarks>
        Notice
    }

    [Serializable]
    public struct HUDMessage
    {
        public HUDMessageType type;
        [TextArea]
        public string message;

        public HUDMessage(HUDMessageType type, string message)
        {
            this.type = type;
            this.message = message;
        }

        public override readonly string ToString() => message;
    }
    
    public static class PlayerHUD
    {
        private static readonly Dictionary<HUDMessageType, HUDTextHandler> HUDHandlers = new();

        internal static void RegisterHUDHandler(HUDTextHandler handler)
        {
            if (HUDHandlers.ContainsKey(handler.HUDIdentifier))
            {
                Debug.LogWarning($"HUD handler for {handler.HUDIdentifier} is already registered. Overwriting with new handler.");
                HUDHandlers[handler.HUDIdentifier] = handler;
            }
            else
            {
                HUDHandlers.Add(handler.HUDIdentifier, handler);
            }
        }

        public static void NewMessage(HUDMessage message)
        {
            if (HUDHandlers.TryGetValue(message.type, out var handler))
                handler.SetText(message.message);
            else
            {
                Debug.LogWarning($"[Player HUD] No HUD handler found for {message.type}");
            }
        }
    }
}
