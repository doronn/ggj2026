using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using BreakingHue.Input;
using BreakingHue.UI;

namespace BreakingHue.Tutorial
{
    /// <summary>
    /// UI component that displays tutorial prompts.
    /// Uses Unity UI Toolkit for rendering.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TutorialPromptUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _promptContainer;
        private Label _titleLabel;
        private VisualElement _messageContainer;
        private Label _keyLabel;
        private Label _actionLabel;
        
        private TutorialStep _currentTutorial;
        private bool _isShowing;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                _uiDocument = gameObject.AddComponent<UIDocument>();
            }
        }

        private void Start()
        {
            // Subscribe to input device changes
            InputManager.OnInputDeviceChanged += OnInputDeviceChanged;
            
            CreateUI();
        }

        private void OnDestroy()
        {
            InputManager.OnInputDeviceChanged -= OnInputDeviceChanged;
        }

        private void CreateUI()
        {
            if (_uiDocument == null)
            {
                Debug.LogWarning("[TutorialPromptUI] UIDocument is null, cannot create UI");
                return;
            }
            
            // Ensure we have PanelSettings
            if (_uiDocument.panelSettings == null)
            {
                Debug.LogWarning("[TutorialPromptUI] UIDocument has no PanelSettings, trying to find one...");
                // Try to find PanelSettings from another UIDocument
                var otherDoc = FindObjectOfType<UIDocument>();
                if (otherDoc != null && otherDoc != _uiDocument && otherDoc.panelSettings != null)
                {
                    _uiDocument.panelSettings = otherDoc.panelSettings;
                    Debug.Log("[TutorialPromptUI] Borrowed PanelSettings from another UIDocument");
                }
                else
                {
                    Debug.LogError("[TutorialPromptUI] No PanelSettings found! Tutorial UI will not work.");
                    return;
                }
            }
            
            _root = _uiDocument.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[TutorialPromptUI] rootVisualElement is null, waiting for next frame...");
                // Schedule UI creation for next frame
                StartCoroutine(CreateUIDelayed());
                return;
            }
            
            // Make root pass through mouse events
            _root.pickingMode = PickingMode.Ignore;
            _root.style.flexGrow = 1;
            
            // Main prompt container - centered on screen
            _promptContainer = new VisualElement();
            _promptContainer.name = "TutorialPromptContainer";
            _promptContainer.style.position = Position.Absolute;
            _promptContainer.style.top = Length.Percent(30);
            _promptContainer.style.left = Length.Percent(50);
            _promptContainer.style.translate = new Translate(Length.Percent(-50), 0);
            _promptContainer.style.minWidth = 300;
            _promptContainer.style.maxWidth = 500;
            _promptContainer.style.flexDirection = FlexDirection.Column;
            _promptContainer.style.alignItems = Align.Center;
            _promptContainer.style.paddingTop = 24;
            _promptContainer.style.paddingBottom = 24;
            _promptContainer.style.paddingLeft = 32;
            _promptContainer.style.paddingRight = 32;
            _promptContainer.style.backgroundColor = new StyleColor(new Color(0.04f, 0.05f, 0.08f, 0.95f));
            _promptContainer.style.borderTopLeftRadius = 16;
            _promptContainer.style.borderTopRightRadius = 16;
            _promptContainer.style.borderBottomLeftRadius = 16;
            _promptContainer.style.borderBottomRightRadius = 16;
            _promptContainer.style.borderTopWidth = 3;
            _promptContainer.style.borderBottomWidth = 3;
            _promptContainer.style.borderLeftWidth = 3;
            _promptContainer.style.borderRightWidth = 3;
            var borderColor = new Color(0f, 0.78f, 1f, 0.7f);
            _promptContainer.style.borderTopColor = new StyleColor(borderColor);
            _promptContainer.style.borderBottomColor = new StyleColor(borderColor);
            _promptContainer.style.borderLeftColor = new StyleColor(borderColor);
            _promptContainer.style.borderRightColor = new StyleColor(borderColor);
            _promptContainer.style.display = DisplayStyle.None;
            _promptContainer.style.opacity = 0;
            
            // Title label
            _titleLabel = new Label();
            _titleLabel.name = "TutorialTitle";
            _titleLabel.style.fontSize = 22;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new StyleColor(new Color(0f, 0.9f, 0.8f, 1f));
            _titleLabel.style.marginBottom = 16;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _promptContainer.Add(_titleLabel);
            
            // Message container (holds key + text)
            _messageContainer = new VisualElement();
            _messageContainer.name = "MessageContainer";
            _messageContainer.style.flexDirection = FlexDirection.Column;
            _messageContainer.style.alignItems = Align.Center;
            _promptContainer.Add(_messageContainer);
            
            // Key label container (will hold styled button)
            _keyLabel = new Label();
            _keyLabel.name = "KeyLabel";
            _keyLabel.style.marginBottom = 12;
            _keyLabel.style.display = DisplayStyle.None;
            _messageContainer.Add(_keyLabel);
            
            // Action/message label
            _actionLabel = new Label();
            _actionLabel.name = "ActionLabel";
            _actionLabel.style.fontSize = 16;
            _actionLabel.style.color = new StyleColor(new Color(0.85f, 0.88f, 0.95f, 0.95f));
            _actionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionLabel.style.whiteSpace = WhiteSpace.Normal;
            _messageContainer.Add(_actionLabel);
            
            _root.Add(_promptContainer);
            
            Debug.Log("[TutorialPromptUI] UI created successfully");
        }
        
        private IEnumerator CreateUIDelayed()
        {
            yield return null; // Wait one frame
            
            _root = _uiDocument.rootVisualElement;
            if (_root != null)
            {
                CreateUIInternal();
            }
            else
            {
                Debug.LogError("[TutorialPromptUI] rootVisualElement still null after delay");
            }
        }
        
        private void CreateUIInternal()
        {
            // Make root pass through mouse events
            _root.pickingMode = PickingMode.Ignore;
            _root.style.flexGrow = 1;
            
            // Main prompt container - centered on screen
            _promptContainer = new VisualElement();
            _promptContainer.name = "TutorialPromptContainer";
            _promptContainer.style.position = Position.Absolute;
            _promptContainer.style.top = Length.Percent(30);
            _promptContainer.style.left = Length.Percent(50);
            _promptContainer.style.translate = new Translate(Length.Percent(-50), 0);
            _promptContainer.style.minWidth = 300;
            _promptContainer.style.maxWidth = 500;
            _promptContainer.style.flexDirection = FlexDirection.Column;
            _promptContainer.style.alignItems = Align.Center;
            _promptContainer.style.paddingTop = 24;
            _promptContainer.style.paddingBottom = 24;
            _promptContainer.style.paddingLeft = 32;
            _promptContainer.style.paddingRight = 32;
            _promptContainer.style.backgroundColor = new StyleColor(new Color(0.04f, 0.05f, 0.08f, 0.95f));
            _promptContainer.style.borderTopLeftRadius = 16;
            _promptContainer.style.borderTopRightRadius = 16;
            _promptContainer.style.borderBottomLeftRadius = 16;
            _promptContainer.style.borderBottomRightRadius = 16;
            _promptContainer.style.borderTopWidth = 3;
            _promptContainer.style.borderBottomWidth = 3;
            _promptContainer.style.borderLeftWidth = 3;
            _promptContainer.style.borderRightWidth = 3;
            var borderColor = new Color(0f, 0.78f, 1f, 0.7f);
            _promptContainer.style.borderTopColor = new StyleColor(borderColor);
            _promptContainer.style.borderBottomColor = new StyleColor(borderColor);
            _promptContainer.style.borderLeftColor = new StyleColor(borderColor);
            _promptContainer.style.borderRightColor = new StyleColor(borderColor);
            _promptContainer.style.display = DisplayStyle.None;
            _promptContainer.style.opacity = 0;
            
            // Title label
            _titleLabel = new Label();
            _titleLabel.name = "TutorialTitle";
            _titleLabel.style.fontSize = 22;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new StyleColor(new Color(0f, 0.9f, 0.8f, 1f));
            _titleLabel.style.marginBottom = 16;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _promptContainer.Add(_titleLabel);
            
            // Message container (holds key + text)
            _messageContainer = new VisualElement();
            _messageContainer.name = "MessageContainer";
            _messageContainer.style.flexDirection = FlexDirection.Column;
            _messageContainer.style.alignItems = Align.Center;
            _promptContainer.Add(_messageContainer);
            
            // Key label container (will hold styled button)
            _keyLabel = new Label();
            _keyLabel.name = "KeyLabel";
            _keyLabel.style.marginBottom = 12;
            _keyLabel.style.display = DisplayStyle.None;
            _messageContainer.Add(_keyLabel);
            
            // Action/message label
            _actionLabel = new Label();
            _actionLabel.name = "ActionLabel";
            _actionLabel.style.fontSize = 16;
            _actionLabel.style.color = new StyleColor(new Color(0.85f, 0.88f, 0.95f, 0.95f));
            _actionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionLabel.style.whiteSpace = WhiteSpace.Normal;
            _messageContainer.Add(_actionLabel);
            
            _root.Add(_promptContainer);
            
            Debug.Log("[TutorialPromptUI] UI created successfully (delayed)");
        }

        /// <summary>
        /// Shows a tutorial prompt.
        /// </summary>
        public void ShowTutorial(TutorialStep tutorial)
        {
            if (_promptContainer == null) return;
            
            _currentTutorial = tutorial;
            _isShowing = true;
            
            // Set title
            _titleLabel.text = tutorial.title;
            
            // Set key label (if action specified)
            if (!string.IsNullOrEmpty(tutorial.actionName))
            {
                // Use InputIconProvider to create a styled button
                UpdateKeyButtonDisplay(tutorial.actionName);
                _keyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _keyLabel.style.display = DisplayStyle.None;
            }
            
            // Set message (replace {key} placeholder)
            string message = tutorial.message;
            if (!string.IsNullOrEmpty(tutorial.actionName))
            {
                message = message.Replace("{key}", InputIconProvider.GetBindingText(tutorial.actionName));
            }
            _actionLabel.text = message;
            
            // Show with fade in
            _promptContainer.style.display = DisplayStyle.Flex;
            _promptContainer.schedule.Execute(() => {
                _promptContainer.style.opacity = 1;
            }).ExecuteLater(50);
        }
        
        private void UpdateKeyButtonDisplay(string actionName)
        {
            // Clear existing content
            _keyLabel.Clear();
            
            // Create new styled button using InputIconProvider
            var styledButton = InputIconProvider.CreateStyledButton(actionName, 20f);
            styledButton.name = "StyledKeyButton";
            
            // The _keyLabel is actually a container now - we make it look like nothing special
            // and let the styled button handle the visuals
            _keyLabel.style.backgroundColor = StyleKeyword.None;
            _keyLabel.style.borderTopWidth = 0;
            _keyLabel.style.borderBottomWidth = 0;
            _keyLabel.style.borderLeftWidth = 0;
            _keyLabel.style.borderRightWidth = 0;
            _keyLabel.style.paddingTop = 0;
            _keyLabel.style.paddingBottom = 0;
            _keyLabel.style.paddingLeft = 0;
            _keyLabel.style.paddingRight = 0;
            
            // _keyLabel is a Label, not a container, so we need to handle this differently
            // Instead, update the text and apply styling directly
            _keyLabel.text = "";
            
            // Actually, since _keyLabel is a Label, let's just update it with InputIconProvider
            InputIconProvider.UpdateStyledButton(_keyLabel, actionName);
            
            // Apply the styled button appearance
            var (bgColor, borderColor, textColor, isRound) = InputIconProvider.GetDeviceButtonStyle(
                InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse);
            
            _keyLabel.text = InputIconProvider.GetBindingText(actionName);
            _keyLabel.style.paddingTop = 10;
            _keyLabel.style.paddingBottom = 10;
            _keyLabel.style.paddingLeft = 18;
            _keyLabel.style.paddingRight = 18;
            _keyLabel.style.fontSize = 20;
            _keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _keyLabel.style.color = new StyleColor(textColor);
            _keyLabel.style.backgroundColor = new StyleColor(bgColor);
            
            float borderRadius = isRound ? 15 : 6;
            _keyLabel.style.borderTopLeftRadius = borderRadius;
            _keyLabel.style.borderTopRightRadius = borderRadius;
            _keyLabel.style.borderBottomLeftRadius = borderRadius;
            _keyLabel.style.borderBottomRightRadius = borderRadius;
            _keyLabel.style.borderTopWidth = 2;
            _keyLabel.style.borderBottomWidth = 5;
            _keyLabel.style.borderLeftWidth = 2;
            _keyLabel.style.borderRightWidth = 2;
            _keyLabel.style.borderTopColor = new StyleColor(borderColor * 1.2f);
            _keyLabel.style.borderBottomColor = new StyleColor(borderColor * 0.8f);
            _keyLabel.style.borderLeftColor = new StyleColor(borderColor);
            _keyLabel.style.borderRightColor = new StyleColor(borderColor);
        }

        /// <summary>
        /// Hides the current tutorial.
        /// </summary>
        public void HideTutorial()
        {
            if (_promptContainer == null || !_isShowing) return;
            
            _isShowing = false;
            
            // Fade out
            _promptContainer.style.opacity = 0;
            _promptContainer.schedule.Execute(() => {
                if (!_isShowing)
                {
                    _promptContainer.style.display = DisplayStyle.None;
                }
            }).ExecuteLater((long)(fadeOutDuration * 1000));
            
            _currentTutorial = null;
        }

        private void OnInputDeviceChanged(InputManager.InputDeviceType deviceType)
        {
            // Update key label if tutorial is showing
            if (_isShowing && _currentTutorial != null && !string.IsNullOrEmpty(_currentTutorial.actionName))
            {
                // Update the styled button with new device styling
                UpdateKeyButtonDisplay(_currentTutorial.actionName);
                
                // Also update message if it has {key} placeholder
                string message = _currentTutorial.message;
                message = message.Replace("{key}", InputIconProvider.GetBindingText(_currentTutorial.actionName));
                _actionLabel.text = message;
            }
        }

        /// <summary>
        /// Checks if a tutorial is currently showing.
        /// </summary>
        public bool IsShowing => _isShowing;
    }
}
