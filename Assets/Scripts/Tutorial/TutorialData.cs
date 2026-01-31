using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreakingHue.Tutorial
{
    /// <summary>
    /// Defines a single tutorial step that can be triggered.
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        /// <summary>
        /// Unique identifier for this tutorial step.
        /// </summary>
        public string id;
        
        /// <summary>
        /// Display title for the tutorial prompt.
        /// </summary>
        public string title;
        
        /// <summary>
        /// Main instruction text.
        /// </summary>
        public string message;
        
        /// <summary>
        /// The input action to show (e.g., "ToggleMask1", "DropMask").
        /// If empty, no key will be shown.
        /// </summary>
        public string actionName;
        
        /// <summary>
        /// How long the tutorial should display (0 = until dismissed/completed).
        /// </summary>
        public float displayDuration = 0f;
        
        /// <summary>
        /// Whether this tutorial should pause the game.
        /// </summary>
        public bool pauseGame = false;
        
        /// <summary>
        /// Priority for display ordering (higher = more important).
        /// </summary>
        public int priority = 0;
    }

    /// <summary>
    /// Save data for tutorial progress.
    /// Serialized with checkpoint data.
    /// </summary>
    [Serializable]
    public class TutorialSaveData
    {
        /// <summary>
        /// IDs of tutorials that have been completed.
        /// </summary>
        public List<string> completedTutorials = new List<string>();
        
        /// <summary>
        /// Creates a deep copy.
        /// </summary>
        public TutorialSaveData Clone()
        {
            return new TutorialSaveData
            {
                completedTutorials = new List<string>(completedTutorials)
            };
        }
        
        /// <summary>
        /// Checks if a tutorial has been completed.
        /// </summary>
        public bool IsCompleted(string tutorialId)
        {
            return completedTutorials.Contains(tutorialId);
        }
        
        /// <summary>
        /// Marks a tutorial as completed.
        /// </summary>
        public void MarkCompleted(string tutorialId)
        {
            if (!completedTutorials.Contains(tutorialId))
            {
                completedTutorials.Add(tutorialId);
            }
        }
        
        /// <summary>
        /// Resets all tutorial progress.
        /// </summary>
        public void Reset()
        {
            completedTutorials.Clear();
        }
    }

    /// <summary>
    /// Static tutorial definitions.
    /// </summary>
    public static class TutorialDefinitions
    {
        // Tutorial IDs
        public const string TUTORIAL_EQUIP_MASK = "equip_mask";
        public const string TUTORIAL_COMBINE_MASKS = "combine_masks";
        public const string TUTORIAL_DROP_MASK = "drop_mask";
        public const string TUTORIAL_PICKUP_MASK = "pickup_mask";
        public const string TUTORIAL_MOVEMENT = "movement";
        
        /// <summary>
        /// Gets all predefined tutorial steps.
        /// </summary>
        public static Dictionary<string, TutorialStep> GetAllTutorials()
        {
            return new Dictionary<string, TutorialStep>
            {
                {
                    TUTORIAL_EQUIP_MASK,
                    new TutorialStep
                    {
                        id = TUTORIAL_EQUIP_MASK,
                        title = "Equip Your Mask",
                        message = "Press {key} to equip your mask and pass through the barrier.",
                        actionName = "ToggleMask1",
                        displayDuration = 0,
                        priority = 10
                    }
                },
                {
                    TUTORIAL_COMBINE_MASKS,
                    new TutorialStep
                    {
                        id = TUTORIAL_COMBINE_MASKS,
                        title = "Combine Colors",
                        message = "This barrier requires multiple colors.\nEquip multiple masks to combine their colors!",
                        actionName = "",
                        displayDuration = 0,
                        priority = 10
                    }
                },
                {
                    TUTORIAL_DROP_MASK,
                    new TutorialStep
                    {
                        id = TUTORIAL_DROP_MASK,
                        title = "Inventory Full",
                        message = "Your inventory is full!\nPress {key} to drop a mask and make room.",
                        actionName = "DropMask",
                        displayDuration = 0,
                        priority = 15
                    }
                },
                {
                    TUTORIAL_PICKUP_MASK,
                    new TutorialStep
                    {
                        id = TUTORIAL_PICKUP_MASK,
                        title = "Mask Collected!",
                        message = "You picked up a mask!\nMasks let you pass through matching color barriers.",
                        actionName = "",
                        displayDuration = 4f,
                        priority = 5
                    }
                },
                {
                    TUTORIAL_MOVEMENT,
                    new TutorialStep
                    {
                        id = TUTORIAL_MOVEMENT,
                        title = "Movement",
                        message = "Use {key} to move around.",
                        actionName = "Move",
                        displayDuration = 3f,
                        priority = 1
                    }
                }
            };
        }
    }
}
