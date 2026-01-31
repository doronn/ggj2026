using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreakingHue.Core
{
    /// <summary>
    /// Singleton manager that handles end game flow.
    /// Triggers the end game scene, resets progress, and returns to main menu.
    /// </summary>
    public class EndGameManager : MonoBehaviour
    {
        private static EndGameManager _instance;
        public static EndGameManager Instance => _instance;
        
        /// <summary>
        /// The currently active end game configuration.
        /// Set when TriggerEndGame is called, used by EndGameController.
        /// </summary>
        public static EndGameConfig ActiveConfig { get; private set; }
        
        /// <summary>
        /// Event fired when the end game is triggered.
        /// </summary>
        public static event Action<EndGameConfig> OnEndGameTriggered;
        
        /// <summary>
        /// Event fired when returning to main menu from end game.
        /// </summary>
        public static event Action OnReturnToMainMenu;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Triggers the end game sequence with the given configuration.
        /// </summary>
        public static void TriggerEndGame(EndGameConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[EndGameManager] Cannot trigger end game - config is null!");
                return;
            }
            
            ActiveConfig = config;
            
            Debug.Log($"[EndGameManager] End game triggered: {config.completionTitle}");
            
            // Reset progress if configured
            if (config.autoResetProgress)
            {
                ResetAllProgress();
            }
            
            OnEndGameTriggered?.Invoke(config);
            
            // Load end game scene
            if (!string.IsNullOrEmpty(config.endGameSceneName))
            {
                SceneManager.LoadScene(config.endGameSceneName);
            }
            else
            {
                Debug.LogWarning("[EndGameManager] No end game scene configured, returning to main menu");
                ReturnToMainMenu();
            }
        }

        /// <summary>
        /// Returns to the main menu from the end game screen.
        /// </summary>
        public static void ReturnToMainMenu()
        {
            string mainMenuScene = ActiveConfig?.mainMenuSceneName ?? "MainMenu";
            
            Debug.Log($"[EndGameManager] Returning to main menu: {mainMenuScene}");
            
            OnReturnToMainMenu?.Invoke();
            
            ActiveConfig = null;
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>
        /// Resets all game progress (checkpoint save file).
        /// </summary>
        public static void ResetAllProgress()
        {
            // Delete the checkpoint save file
            string savePath = Path.Combine(Application.persistentDataPath, "checkpoint.json");
            
            if (File.Exists(savePath))
            {
                try
                {
                    File.Delete(savePath);
                    Debug.Log($"[EndGameManager] Deleted save file: {savePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EndGameManager] Failed to delete save file: {e.Message}");
                }
            }
            
            Debug.Log("[EndGameManager] All game progress has been reset");
        }

        /// <summary>
        /// Checks if an end game is currently active.
        /// </summary>
        public static bool IsEndGameActive => ActiveConfig != null;
    }
}
