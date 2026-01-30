using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using BreakingHue.Input;

namespace BreakingHue.UI
{
    /// <summary>
    /// Controls the pause menu UI.
    /// Pauses game time and provides Resume and Exit to Menu options.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        
        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool pauseTimeOnOpen = true;

        private VisualElement _root;
        private VisualElement _menuPanel;
        private Button _resumeButton;
        private Button _exitButton;
        
        private InputAction _pauseAction;
        private bool _isPaused;
        private float _previousTimeScale;

        /// <summary>
        /// Event fired when the game is paused.
        /// </summary>
        public static event Action OnGamePaused;
        
        /// <summary>
        /// Event fired when the game is resumed.
        /// </summary>
        public static event Action OnGameResumed;

        /// <summary>
        /// Whether the game is currently paused.
        /// </summary>
        public bool IsPaused => _isPaused;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            SetupUI();
            SetupInput();
            
            // Start hidden
            Hide();
        }

        private void OnDestroy()
        {
            // Restore time scale if destroyed while paused
            if (_isPaused)
            {
                Time.timeScale = _previousTimeScale;
            }
            
            _pauseAction?.Disable();
            _pauseAction?.Dispose();
        }

        private void SetupUI()
        {
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            
            if (_root == null || _root.Q<VisualElement>("PauseMenuPanel") == null)
            {
                CreateUIFromCode();
            }
            else
            {
                _menuPanel = _root.Q<VisualElement>("PauseMenuPanel");
                _resumeButton = _root.Q<Button>("ResumeButton");
                _exitButton = _root.Q<Button>("ExitButton");
            }
            
            // Setup button callbacks
            if (_resumeButton != null)
            {
                _resumeButton.clicked += OnResumeClicked;
            }
            
            if (_exitButton != null)
            {
                _exitButton.clicked += OnExitClicked;
            }
        }

        private void CreateUIFromCode()
        {
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            _root.Clear();
            
            // Dark overlay
            var overlay = new VisualElement();
            overlay.name = "Overlay";
            overlay.style.position = Position.Absolute;
            overlay.style.top = 0;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            
            // Menu panel
            _menuPanel = new VisualElement();
            _menuPanel.name = "PauseMenuPanel";
            _menuPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            _menuPanel.style.paddingTop = 30;
            _menuPanel.style.paddingBottom = 30;
            _menuPanel.style.paddingLeft = 50;
            _menuPanel.style.paddingRight = 50;
            _menuPanel.style.borderTopLeftRadius = 12;
            _menuPanel.style.borderTopRightRadius = 12;
            _menuPanel.style.borderBottomLeftRadius = 12;
            _menuPanel.style.borderBottomRightRadius = 12;
            _menuPanel.style.alignItems = Align.Center;
            
            // Title
            var title = new Label("PAUSED");
            title.name = "Title";
            title.style.fontSize = 36;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 30;
            
            // Resume button
            _resumeButton = new Button();
            _resumeButton.name = "ResumeButton";
            _resumeButton.text = "Resume";
            StyleButton(_resumeButton, new Color(0.2f, 0.6f, 0.3f, 1f));
            _resumeButton.style.marginBottom = 15;
            
            // Exit button
            _exitButton = new Button();
            _exitButton.name = "ExitButton";
            _exitButton.text = "Exit to Menu";
            StyleButton(_exitButton, new Color(0.6f, 0.2f, 0.2f, 1f));
            
            _menuPanel.Add(title);
            _menuPanel.Add(_resumeButton);
            _menuPanel.Add(_exitButton);
            
            overlay.Add(_menuPanel);
            _root.Add(overlay);
            
            // Setup callbacks
            _resumeButton.clicked += OnResumeClicked;
            _exitButton.clicked += OnExitClicked;
        }

        private void StyleButton(Button button, Color backgroundColor)
        {
            button.style.width = 200;
            button.style.height = 50;
            button.style.fontSize = 18;
            button.style.color = Color.white;
            button.style.backgroundColor = backgroundColor;
            button.style.borderTopLeftRadius = 8;
            button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = 8;
            button.style.borderBottomRightRadius = 8;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void SetupInput()
        {
            // Try to get from InputManager
            if (InputManager.Instance != null)
            {
                _pauseAction = InputManager.Instance.GetPlayerAction("Pause");
            }
            
            // Fallback: create manual input action
            if (_pauseAction == null)
            {
                _pauseAction = new InputAction("Pause", InputActionType.Button);
                _pauseAction.AddBinding("<Keyboard>/escape");
                _pauseAction.AddBinding("<Gamepad>/start");
            }
            
            _pauseAction.performed += OnPausePressed;
            _pauseAction.Enable();
        }

        private void OnPausePressed(InputAction.CallbackContext context)
        {
            if (_isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        private void OnResumeClicked()
        {
            Resume();
        }

        private void OnExitClicked()
        {
            // Resume time before loading new scene
            if (pauseTimeOnOpen)
            {
                Time.timeScale = _previousTimeScale;
            }
            
            _isPaused = false;
            
            // Load main menu
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>
        /// Pauses the game and shows the pause menu.
        /// </summary>
        public void Pause()
        {
            if (_isPaused) return;
            
            _isPaused = true;
            
            if (pauseTimeOnOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            
            Show();
            
            OnGamePaused?.Invoke();
            Debug.Log("[PauseMenu] Game paused");
        }

        /// <summary>
        /// Resumes the game and hides the pause menu.
        /// </summary>
        public void Resume()
        {
            if (!_isPaused) return;
            
            _isPaused = false;
            
            if (pauseTimeOnOpen)
            {
                Time.timeScale = _previousTimeScale;
            }
            
            Hide();
            
            OnGameResumed?.Invoke();
            Debug.Log("[PauseMenu] Game resumed");
        }

        /// <summary>
        /// Shows the pause menu UI.
        /// </summary>
        public void Show()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// Hides the pause menu UI.
        /// </summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Toggles the pause state.
        /// </summary>
        public void Toggle()
        {
            if (_isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }
}
