using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using BreakingHue.Input;

namespace BreakingHue.UI
{
    /// <summary>
    /// Controls the main menu UI using UI Toolkit.
    /// Handles Play button, Reset Game with confirmation, and controls display.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string gameSceneName = "World";
        
        [Header("Save Settings")]
        [SerializeField] private string checkpointFileName = "checkpoint.json";

        private UIDocument _uiDocument;
        private Button _playButton;
        private Button _resetButton;
        private Button _quitButton;
        private VisualElement _confirmationDialog;
        private Button _confirmYesButton;
        private Button _confirmNoButton;
        private Label _controlsLabel;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            // Subscribe to input device changes
            InputManager.OnInputDeviceChanged += OnInputDeviceChanged;
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
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
            if (_quitButton != null) _quitButton.clicked -= OnQuitClicked;
            if (_confirmYesButton != null) _confirmYesButton.clicked -= OnConfirmYes;
            if (_confirmNoButton != null) _confirmNoButton.clicked -= OnConfirmNo;
            
            InputManager.OnInputDeviceChanged -= OnInputDeviceChanged;
        }

        private void OnUIReady(GeometryChangedEvent evt)
        {
            _uiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnUIReady);
            SetupUI();
        }

        private void SetupUI()
        {
            var root = _uiDocument.rootVisualElement;
            
            // Try to find existing elements
            _playButton = root.Q<Button>("PlayButton");
            _resetButton = root.Q<Button>("ResetButton");
            _quitButton = root.Q<Button>("QuitButton");
            _confirmationDialog = root.Q<VisualElement>("ConfirmationDialog");
            _confirmYesButton = root.Q<Button>("ConfirmYesButton");
            _confirmNoButton = root.Q<Button>("ConfirmNoButton");
            _controlsLabel = root.Q<Label>("ControlsLabel");
            
            // If elements not found, create them dynamically
            if (_playButton == null)
            {
                CreateDynamicUI(root);
            }
            else
            {
                // FIX: Apply interactive styles to UXML-based buttons
                ApplyButtonInteractiveStyles(_playButton, new Color(0.24f, 0.47f, 0.78f));
                if (_resetButton != null) ApplyButtonInteractiveStyles(_resetButton, new Color(0.6f, 0.3f, 0.3f));
                if (_quitButton != null) ApplyButtonInteractiveStyles(_quitButton, new Color(0.3f, 0.3f, 0.3f));
                if (_confirmYesButton != null) ApplyButtonInteractiveStyles(_confirmYesButton, new Color(0.7f, 0.2f, 0.2f));
                if (_confirmNoButton != null) ApplyButtonInteractiveStyles(_confirmNoButton, new Color(0.3f, 0.3f, 0.35f));
            }
            
            // Setup callbacks
            if (_playButton != null) _playButton.clicked += OnPlayClicked;
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_quitButton != null) _quitButton.clicked += OnQuitClicked;
            if (_confirmYesButton != null) _confirmYesButton.clicked += OnConfirmYes;
            if (_confirmNoButton != null) _confirmNoButton.clicked += OnConfirmNo;
            
            // Hide confirmation dialog initially
            if (_confirmationDialog != null)
            {
                _confirmationDialog.style.display = DisplayStyle.None;
            }
            
            // Update controls display
            UpdateControlsDisplay();
            
            // Set initial focus on Play button for keyboard/controller navigation
            if (_playButton != null)
            {
                _playButton.schedule.Execute(() => _playButton.Focus()).ExecuteLater(100);
            }
        }

        private void CreateDynamicUI(VisualElement root)
        {
            root.Clear();
            
            // Main container
            var container = new VisualElement();
            container.name = "MainContainer";
            container.style.flexGrow = 1;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.backgroundColor = new Color(0.08f, 0.08f, 0.14f);
            
            // Title
            var title = new Label("BREAKING HUE");
            title.name = "Title";
            title.style.fontSize = 64;
            title.style.color = Color.white;
            title.style.marginBottom = 10;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            // Subtitle
            var subtitle = new Label("RYB Color Puzzle");
            subtitle.name = "Subtitle";
            subtitle.style.fontSize = 24;
            subtitle.style.color = new Color(1f, 0.78f, 0.39f);
            subtitle.style.marginBottom = 40;
            
            // Description
            var description = new Label("Toggle masks to combine colors and pass through barriers");
            description.name = "Description";
            description.style.fontSize = 18;
            description.style.color = new Color(0.7f, 0.7f, 0.7f);
            description.style.marginBottom = 60;
            
            // Buttons container
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.alignItems = Align.Center;
            
            // Play button
            _playButton = new Button();
            _playButton.name = "PlayButton";
            _playButton.text = "PLAY";
            StyleMenuButton(_playButton, new Color(0.24f, 0.47f, 0.78f), true);
            _playButton.style.marginBottom = 15;
            
            // Reset button
            _resetButton = new Button();
            _resetButton.name = "ResetButton";
            _resetButton.text = "Reset Progress";
            StyleMenuButton(_resetButton, new Color(0.6f, 0.3f, 0.3f), false);
            _resetButton.style.marginBottom = 15;
            
            // Quit button
            _quitButton = new Button();
            _quitButton.name = "QuitButton";
            _quitButton.text = "Quit";
            StyleMenuButton(_quitButton, new Color(0.3f, 0.3f, 0.3f), false);
            
            buttonsContainer.Add(_playButton);
            buttonsContainer.Add(_resetButton);
            buttonsContainer.Add(_quitButton);
            
            // Controls info
            var controlsContainer = new VisualElement();
            controlsContainer.style.marginTop = 50;
            controlsContainer.style.alignItems = Align.Center;
            
            var controlsTitle = new Label("Controls:");
            controlsTitle.style.fontSize = 16;
            controlsTitle.style.color = Color.white;
            controlsTitle.style.marginBottom = 10;
            
            _controlsLabel = new Label();
            _controlsLabel.name = "ControlsLabel";
            _controlsLabel.style.fontSize = 14;
            _controlsLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            
            controlsContainer.Add(controlsTitle);
            controlsContainer.Add(_controlsLabel);
            
            container.Add(title);
            container.Add(subtitle);
            container.Add(description);
            container.Add(buttonsContainer);
            container.Add(controlsContainer);
            
            // Confirmation dialog
            _confirmationDialog = CreateConfirmationDialog();
            
            root.Add(container);
            root.Add(_confirmationDialog);
        }

        private VisualElement CreateConfirmationDialog()
        {
            var overlay = new VisualElement();
            overlay.name = "ConfirmationDialog";
            overlay.style.position = Position.Absolute;
            overlay.style.top = 0;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.8f);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.display = DisplayStyle.None;
            
            var panel = new VisualElement();
            panel.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.98f);
            panel.style.paddingTop = 30;
            panel.style.paddingBottom = 30;
            panel.style.paddingLeft = 40;
            panel.style.paddingRight = 40;
            panel.style.borderTopLeftRadius = 12;
            panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = 12;
            panel.style.borderBottomRightRadius = 12;
            panel.style.alignItems = Align.Center;
            
            var message = new Label("Reset all progress?\nThis cannot be undone.");
            message.style.fontSize = 20;
            message.style.color = Color.white;
            message.style.marginBottom = 30;
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            
            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            
            _confirmYesButton = new Button();
            _confirmYesButton.name = "ConfirmYesButton";
            _confirmYesButton.text = "Yes, Reset";
            StyleDialogButton(_confirmYesButton, new Color(0.7f, 0.2f, 0.2f));
            _confirmYesButton.style.marginRight = 15;
            
            _confirmNoButton = new Button();
            _confirmNoButton.name = "ConfirmNoButton";
            _confirmNoButton.text = "Cancel";
            StyleDialogButton(_confirmNoButton, new Color(0.3f, 0.3f, 0.35f));
            
            buttonsRow.Add(_confirmYesButton);
            buttonsRow.Add(_confirmNoButton);
            
            panel.Add(message);
            panel.Add(buttonsRow);
            
            overlay.Add(panel);
            
            return overlay;
        }

        private void StyleMenuButton(Button button, Color backgroundColor, bool isPrimary)
        {
            button.style.width = isPrimary ? 250 : 200;
            button.style.height = isPrimary ? 60 : 45;
            button.style.fontSize = isPrimary ? 28 : 18;
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
            
            if (isPrimary)
            {
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            
            // Apply interactive styles (same as pause menu)
            ApplyButtonInteractiveStyles(button, backgroundColor);
        }
        
        private void StyleDialogButton(Button button, Color backgroundColor)
        {
            button.style.width = 120;
            button.style.height = 45;
            button.style.fontSize = 16;
            button.style.backgroundColor = backgroundColor;
            button.style.color = Color.white;
            button.style.borderTopLeftRadius = 8;
            button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = 8;
            button.style.borderBottomRightRadius = 8;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            
            // Apply interactive styles
            ApplyButtonInteractiveStyles(button, backgroundColor);
        }
        
        private void ApplyButtonInteractiveStyles(Button button, Color backgroundColor)
        {
            button.focusable = true;
            button.pickingMode = PickingMode.Position;
            
            Color originalBg = backgroundColor;
            
            // Focus effect: WHITE background with CYAN border (matches pause menu)
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

        private void UpdateControlsDisplay()
        {
            if (_controlsLabel == null) return;
            
            _controlsLabel.text = InputIconProvider.GetControlsSummary();
        }

        private void OnInputDeviceChanged(InputManager.InputDeviceType deviceType)
        {
            UpdateControlsDisplay();
        }

        private void OnPlayClicked()
        {
            Debug.Log("[MainMenuController] Starting game...");
            SceneManager.LoadScene(gameSceneName);
        }

        private void OnResetClicked()
        {
            // Show confirmation dialog
            if (_confirmationDialog != null)
            {
                _confirmationDialog.style.display = DisplayStyle.Flex;
            }
        }

        private void OnConfirmYes()
        {
            // Reset progress
            ResetAllProgress();
            
            // Hide dialog
            if (_confirmationDialog != null)
            {
                _confirmationDialog.style.display = DisplayStyle.None;
            }
        }

        private void OnConfirmNo()
        {
            // Hide dialog
            if (_confirmationDialog != null)
            {
                _confirmationDialog.style.display = DisplayStyle.None;
            }
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenuController] Quitting game...");
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ResetAllProgress()
        {
            // Delete checkpoint file
            string savePath = Path.Combine(Application.persistentDataPath, checkpointFileName);
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log($"[MainMenuController] Deleted checkpoint file: {savePath}");
            }
            
            // Clear PlayerPrefs if used
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            
            Debug.Log("[MainMenuController] All progress has been reset");
        }
    }
}
