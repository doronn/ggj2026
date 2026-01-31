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
        private float _previousTimeScale = 1f;
        private float _lastPauseToggleTime;
        private const float PauseToggleCooldown = 0.2f;

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
            // FIX: Ensure time scale is 1 when scene starts (fixes return from menu bug)
            Time.timeScale = 1f;
            _previousTimeScale = 1f;
            
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
            
            // Apply style callbacks to buttons regardless of how they were created (UXML or code)
            if (_resumeButton != null)
            {
                _resumeButton.clicked += OnResumeClicked;
                ApplyButtonStyles(_resumeButton, new Color(0.2f, 0.6f, 0.3f, 1f)); // Green
            }
            
            if (_exitButton != null)
            {
                _exitButton.clicked += OnExitClicked;
                ApplyButtonStyles(_exitButton, new Color(0.6f, 0.2f, 0.2f, 1f)); // Red
            }
            
            // Set sort order higher to ensure pause menu is on top
            if (uiDocument.panelSettings != null)
            {
                uiDocument.sortingOrder = 100;
            }
            
            // Ensure picking mode is set for mouse interaction
            if (_root != null)
            {
                _root.pickingMode = PickingMode.Position;
            }
        }
        
        /// <summary>
        /// Applies interactive styles to a button (focus, hover effects).
        /// Called for both UXML-based and code-created buttons.
        /// </summary>
        private void ApplyButtonStyles(Button button, Color backgroundColor)
        {
            button.focusable = true;
            button.pickingMode = PickingMode.Position;
            
            // Store original color
            Color originalBg = backgroundColor;
            
            // Visible focus - WHITE background with CYAN border
            button.RegisterCallback<FocusInEvent>(evt => {
                button.style.borderTopWidth = 6;
                button.style.borderBottomWidth = 6;
                button.style.borderLeftWidth = 6;
                button.style.borderRightWidth = 6;
                button.style.borderTopColor = new Color(0f, 1f, 1f, 1f); // Cyan
                button.style.borderBottomColor = new Color(0f, 1f, 1f, 1f);
                button.style.borderLeftColor = new Color(0f, 1f, 1f, 1f);
                button.style.borderRightColor = new Color(0f, 1f, 1f, 1f);
                button.style.scale = new Scale(new Vector3(1.15f, 1.15f, 1f));
                button.style.backgroundColor = new Color(1f, 1f, 1f, 1f); // White
                button.style.color = Color.black;
            });
            
            button.RegisterCallback<FocusOutEvent>(evt => {
                button.style.borderTopWidth = 0;
                button.style.borderBottomWidth = 0;
                button.style.borderLeftWidth = 0;
                button.style.borderRightWidth = 0;
                button.style.scale = new Scale(Vector3.one);
                button.style.backgroundColor = originalBg;
                button.style.color = Color.white;
            });
            
            // Hover effect for mouse
            button.RegisterCallback<MouseEnterEvent>(evt => {
                if (button.focusController?.focusedElement != button)
                {
                    button.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f));
                    button.style.backgroundColor = new Color(
                        Mathf.Min(1f, originalBg.r + 0.15f),
                        Mathf.Min(1f, originalBg.g + 0.15f),
                        Mathf.Min(1f, originalBg.b + 0.15f),
                        1f
                    );
                }
            });
            
            button.RegisterCallback<MouseLeaveEvent>(evt => {
                if (button.focusController?.focusedElement != button)
                {
                    button.style.scale = new Scale(Vector3.one);
                    button.style.backgroundColor = originalBg;
                }
            });
        }

        private void CreateUIFromCode()
        {
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            _root.Clear();
            
            // FIX: Ensure root can receive pointer events
            _root.pickingMode = PickingMode.Position;
            
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
            // FIX: Overlay should pass through clicks to children
            overlay.pickingMode = PickingMode.Position;
            
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
            // FIX: Ensure panel can receive pointer events
            _menuPanel.pickingMode = PickingMode.Position;
            
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
            StyleButtonBasics(_resumeButton, new Color(0.2f, 0.6f, 0.3f, 1f));
            _resumeButton.style.marginBottom = 15;
            
            // Exit button
            _exitButton = new Button();
            _exitButton.name = "ExitButton";
            _exitButton.text = "Exit to Menu";
            StyleButtonBasics(_exitButton, new Color(0.6f, 0.2f, 0.2f, 1f));
            
            _menuPanel.Add(title);
            _menuPanel.Add(_resumeButton);
            _menuPanel.Add(_exitButton);
            
            overlay.Add(_menuPanel);
            _root.Add(overlay);
            
            // Note: Callbacks are registered in SetupUI via ApplyButtonStyles
        }

        /// <summary>
        /// Sets basic visual styling for a button (no event callbacks).
        /// Event callbacks are added by ApplyButtonStyles in SetupUI.
        /// </summary>
        private void StyleButtonBasics(Button button, Color backgroundColor)
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
            // FIX: Add cooldown to prevent multiple rapid toggles
            if (Time.unscaledTime - _lastPauseToggleTime < PauseToggleCooldown)
            {
                return;
            }
            _lastPauseToggleTime = Time.unscaledTime;
            
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
            // FIX: Always reset time scale to 1 when exiting to menu
            Time.timeScale = 1f;
            
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
                // Only capture previous time scale if it's not already 0
                if (Time.timeScale > 0f)
                {
                    _previousTimeScale = Time.timeScale;
                }
                else
                {
                    _previousTimeScale = 1f;
                }
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
                
                // Focus the first button for keyboard navigation
                if (_resumeButton != null)
                {
                    _resumeButton.Focus();
                }
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
