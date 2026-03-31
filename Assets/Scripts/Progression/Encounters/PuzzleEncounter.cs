using System.Collections.Generic;
using UnityEngine;

namespace Progression.Encounters
{
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.mx9wqx5qgrio")]
    public class PuzzleEncounter : BasicEncounter
    {
        /*
         * reminder to self: I need to add a way to call HandleOnComplete from the basicEncounter
         * It should be fine for now though.
         * I also just need to generally give this script a lookover too to make sure its functioning optimal.
         */

        protected override Color DebugColor => Color.purple;

        #region Inspector Setup
        [SerializeField] private string objectiveText = "";
        [Header("Optional Overrides")]
        [SerializeField] private PuzzlePart overridePuzzlePart;
        [SerializeField] private PuzzleInteraction[] overrideInteractPoints;
        #endregion

        private PuzzlePart part;
        private IConsoleSelectable consoleSelectable;
        private PuzzleInteraction[] interactPoints;

        protected override void SetupEncounter()
        {
            interactPoints = (overrideInteractPoints != null && overrideInteractPoints.Length > 0)
                ? overrideInteractPoints
                : GetComponentsInChildren<PuzzleInteraction>();

            part = ResolvePuzzlePart(interactPoints);
            consoleSelectable = part as IConsoleSelectable;

            if (part == null)
                return;

            if (interactPoints == null || interactPoints.Length == 0)
            {
                Debug.LogError($"[PuzzleEncounter] No {nameof(PuzzleInteraction)} scripts found in child objects in encounter {gameObject.name}.");
                return;
            }

            foreach (var interactPoint in interactPoints)
            {
                if (interactPoint == null)
                    continue;

                if (consoleSelectable != null)
                    interactPoint.ButtonPressedWithSender += consoleSelectable.ConsoleInteracted;
                else
                    interactPoint.ButtonPressed += part.ConsoleInteracted;
            }
        }

        private PuzzlePart ResolvePuzzlePart(PuzzleInteraction[] interactionPoints)
        {
            if (overridePuzzlePart != null)
                return overridePuzzlePart;

            if (interactionPoints != null)
            {
                for (int i = 0; i < interactionPoints.Length; i++)
                {
                    PuzzleInteraction interactionPoint = interactionPoints[i];
                    if (interactionPoint == null)
                        continue;

                    PuzzlePart parentPart = interactionPoint.GetComponentInParent<PuzzlePart>();
                    if (parentPart != null)
                        return parentPart;
                }
            }

            return FindPieces<PuzzlePart>();
        }

        protected override void CleanupEncounter()
        {
            if (part != null && interactPoints != null)
            {
                foreach (var interactPoint in interactPoints)
                {
                    if (interactPoint == null)
                        continue;

                    if (consoleSelectable != null)
                        interactPoint.ButtonPressedWithSender -= consoleSelectable.ConsoleInteracted;
                    else
                        interactPoint.ButtonPressed -= part.ConsoleInteracted;
                }
            }

            part = null;
            consoleSelectable = null;
            interactPoints = null;

            base.CleanupEncounter();
        }

        protected override void PlayerEnteredZone()
        {
            base.PlayerEnteredZone();

            if (!string.IsNullOrEmpty(objectiveText))
                InvokeUpdateObjective(objectiveText);
        }

        /// <summary>
        /// Generic method to find the first component of type T in the child objects of this encounter. 
        /// Logs an error if none are found, and a warning if multiple are found (using the first one in that case).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private T FindPieces<T>() where T : Component
        {
            T[] pieces = GetComponentsInChildren<T>();
            if (pieces.Length == 0)
            {
                Debug.LogError($"[PuzzleEncounter] No {typeof(T).Name} scripts found in child objects in encounter {gameObject.name}.");
                return null;
            }

            else if (pieces.Length > 1)
                Debug.LogWarning($"[PuzzleEncounter] Multiple {typeof(T).Name} scripts found in child objects of encounter {gameObject.name}. Using the first one found: {pieces[0].name}.");
            
            return pieces[0];
        }
    }
}
