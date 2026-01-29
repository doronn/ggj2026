using UnityEngine;
using UnityEngine.SceneManagement;
using BreakingHue.Gameplay;

namespace BreakingHue.Core
{
    /// <summary>
    /// Manages game state and scene transitions.
    /// Listens to ExitGoal events for level completion.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private string menuSceneName = "MainMenu";

        [Header("Settings")]
        [SerializeField] private float levelCompleteDelay = 1f;

        private bool _levelComplete;

        private void OnEnable()
        {
            ExitGoal.OnPlayerReachedExit += OnLevelComplete;
        }

        private void OnDisable()
        {
            ExitGoal.OnPlayerReachedExit -= OnLevelComplete;
        }

        /// <summary>
        /// Called when the player reaches the exit goal.
        /// </summary>
        private void OnLevelComplete()
        {
            if (_levelComplete) return; // Prevent multiple triggers
            _levelComplete = true;

            Debug.Log("[GameManager] Level Complete!");
            
            // For MVP: Return to menu after a short delay
            // In a full game, this would show a win screen, save progress, etc.
            Invoke(nameof(ReturnToMenu), levelCompleteDelay);
        }

        /// <summary>
        /// Loads the main menu scene.
        /// </summary>
        public void ReturnToMenu()
        {
            SceneManager.LoadScene(menuSceneName);
        }

        /// <summary>
        /// Starts/restarts the game by loading the game scene.
        /// </summary>
        public void StartGame()
        {
            _levelComplete = false;
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>
        /// Restarts the current level.
        /// </summary>
        public void RestartLevel()
        {
            _levelComplete = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
