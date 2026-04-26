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
        /// Represents a secondary goal or target. Will be listed underneath the main objective.
        /// </summary>
        SubObjective,

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
      //  private static readonly ObjectiveManager objectiveManager = ObjectiveManager.Instance;

        private static readonly Dictionary<HUDMessageType, HUDTextHandler> HUDHandlers = new();
        public static SubobjectiveHandler subObjectiveHandler { get; private set; }

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

        internal static void RegisterSubObjectiveHandler(SubobjectiveHandler handler)
        {
            subObjectiveHandler = handler;
        }

        public static void NewMessage(HUDMessage message)
        {
            switch (message.type)
            {
                case HUDMessageType.Objective:
                    if (HUDHandlers.TryGetValue(message.type, out var handler))
                    {
                        handler.SetText(message.message);
                    }
                    else
                    {
                        Debug.LogWarning($"No HUD handler registered for {message.type}. Cannot display message: {message.message}");
                    }
                    break;

                case HUDMessageType.SubObjective:
                    break;

                case HUDMessageType.Notice:
                    if (HUDHandlers.TryGetValue(HUDMessageType.Notice, out var noticeHandler))
                    {
                        noticeHandler.SetText(message.message);
                        // Example: fade out after 3 seconds
                        noticeHandler.FadeOutText(3f);
                    }
                    else
                    {
                        Debug.LogWarning($"No HUD handler registered for Notice messages. Cannot display notice: {message.message}");
                    }
                    break;
                default:
                    Debug.LogWarning($"Unhandled HUD message type: {message.type}. Message: {message.message}");
                    break;
            }
        }
    }
}
