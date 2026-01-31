using UnityEngine;

namespace BreakingHue.Core
{
    /// <summary>
    /// Configuration for end game portals.
    /// Defines what happens when the player reaches an end game destination.
    /// </summary>
    [CreateAssetMenu(fileName = "EndGameConfig", menuName = "Breaking Hue/End Game Config")]
    public class EndGameConfig : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("Title shown on the end game screen")]
        public string completionTitle = "Congratulations!";
        
        [TextArea(3, 10)]
        [Tooltip("Main completion message")]
        public string completionText = "You have completed Breaking Hue!\n\nThank you for playing!";
        
        [Header("Scene")]
        [Tooltip("Name of the scene to load for the end game screen")]
        public string endGameSceneName = "EndGame";
        
        [Tooltip("Name of the main menu scene to return to")]
        public string mainMenuSceneName = "MainMenu";
        
        [Header("Options")]
        [Tooltip("Automatically reset game progress when reaching the end")]
        public bool autoResetProgress = true;
        
        [Tooltip("Delay before showing the end game content")]
        public float displayDelay = 0.5f;
        
        [Header("Audio (Optional)")]
        [Tooltip("Sound to play when reaching the end")]
        public AudioClip completionSound;
        
        [Tooltip("Background music for the end screen")]
        public AudioClip endScreenMusic;
    }
}
