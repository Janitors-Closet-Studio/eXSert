using UnityEngine;

namespace UIandUXSystems.HUD
{
    public static class PlayerHUD
    {
        internal static ObjectiveHandler objectiveHandler;

        public static void SetObjective(string newObjective)
        {
            if (objectiveHandler != null)
                objectiveHandler.SetObjective(newObjective);
        }
    }
}
