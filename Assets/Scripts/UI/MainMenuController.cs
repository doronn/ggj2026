using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BreakingHue.UI
{
    /// <summary>
    /// Controls the main menu UI using UI Toolkit.
    /// Handles the Play button to start the game.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string gameSceneName = "World";

        private UIDocument _uiDocument;
        private Button _playButton;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Wait for UI to be ready
            if (_uiDocument.rootVisualElement != null)
            {
                SetupUI();
            }
            else
            {
                _uiDocument.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnUIReady);
            }
        }

        private void OnDisable()
        {
            if (_playButton != null)
            {
                _playButton.clicked -= OnPlayClicked;
            }
        }

        private void OnUIReady(GeometryChangedEvent evt)
        {
            _uiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnUIReady);
            SetupUI();
        }

        private void SetupUI()
        {
            var root = _uiDocument.rootVisualElement;
            
            _playButton = root.Q<Button>("PlayButton");
            if (_playButton != null)
            {
                _playButton.clicked += OnPlayClicked;
            }
            else
            {
                Debug.LogWarning("[MainMenuController] PlayButton not found in UXML!");
            }
        }

        private void OnPlayClicked()
        {
            Debug.Log("[MainMenuController] Starting game...");
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
