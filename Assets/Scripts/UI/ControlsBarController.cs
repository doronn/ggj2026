using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Input;
using BreakingHue.Gameplay;

namespace BreakingHue.UI
{
    /// <summary>
    /// Controls the bottom controls bar UI that shows contextual control hints.
    /// Auto-updates based on current input device (keyboard vs controller).
    /// Includes self-destruct progress bar.
    /// </summary>
    public class ControlsBarController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        
        [Header("Settings")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private float progressBarPulseSpeed = 2f;

        private VisualElement _root;
        private VisualElement _controlsContainer;
        private VisualElement _progressBarContainer;
        private VisualElement _progressBarFill;
        private Label _restartLabel;
        
        // Control hint elements
        private VisualElement _moveHint;
        private VisualElement _lookHint;
        private VisualElement _mask1Hint;
        private VisualElement _mask2Hint;
        private VisualElement _mask3Hint;
        private VisualElement _dropHint;
        private VisualElement _restartHint;
        private VisualElement _pauseHint;

        private float _currentProgress;
        private bool _isShowingProgress;

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
            
            // Subscribe to input device changes
            InputManager.OnInputDeviceChanged += OnInputDeviceChanged;
            
            // Subscribe to self-destruct events
            SelfDestructController.OnSelfDestructStarted += OnSelfDestructStarted;
            SelfDestructController.OnSelfDestructProgress += OnSelfDestructProgress;
            SelfDestructController.OnSelfDestructCancelled += OnSelfDestructCancelled;
            SelfDestructController.OnSelfDestructTriggered += OnSelfDestructTriggered;
            
            // Initial update
            UpdateControlHints();
            
            if (!showOnStart)
            {
                Hide();
            }
        }

        private void OnDestroy()
        {
            InputManager.OnInputDeviceChanged -= OnInputDeviceChanged;
            SelfDestructController.OnSelfDestructStarted -= OnSelfDestructStarted;
            SelfDestructController.OnSelfDestructProgress -= OnSelfDestructProgress;
            SelfDestructController.OnSelfDestructCancelled -= OnSelfDestructCancelled;
            SelfDestructController.OnSelfDestructTriggered -= OnSelfDestructTriggered;
        }

        private void Update()
        {
            // Animate progress bar pulse
            if (_isShowingProgress && _progressBarFill != null)
            {
                float pulse = 0.8f + Mathf.Sin(Time.time * progressBarPulseSpeed) * 0.2f;
                _progressBarFill.style.opacity = pulse;
            }
        }

        private void SetupUI()
        {
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            
            if (_root == null)
            {
                CreateUIFromCode();
                return;
            }
            
            // Try to find elements from UXML
            _controlsContainer = _root.Q<VisualElement>("ControlsContainer");
            _progressBarContainer = _root.Q<VisualElement>("ProgressBarContainer");
            _progressBarFill = _root.Q<VisualElement>("ProgressBarFill");
            
            // If no UXML, create from code
            if (_controlsContainer == null)
            {
                CreateUIFromCode();
            }
        }

        private void CreateUIFromCode()
        {
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            _root.Clear();
            
            // Main container at bottom
            var mainContainer = new VisualElement();
            mainContainer.name = "ControlsBarMain";
            mainContainer.style.position = Position.Absolute;
            mainContainer.style.bottom = 0;
            mainContainer.style.left = 0;
            mainContainer.style.right = 0;
            mainContainer.style.paddingBottom = 10;
            mainContainer.style.paddingLeft = 20;
            mainContainer.style.paddingRight = 20;
            mainContainer.style.paddingTop = 10;
            mainContainer.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            
            // Progress bar container (hidden by default)
            _progressBarContainer = new VisualElement();
            _progressBarContainer.name = "ProgressBarContainer";
            _progressBarContainer.style.height = 6;
            _progressBarContainer.style.marginBottom = 8;
            _progressBarContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            _progressBarContainer.style.borderTopLeftRadius = 3;
            _progressBarContainer.style.borderTopRightRadius = 3;
            _progressBarContainer.style.borderBottomLeftRadius = 3;
            _progressBarContainer.style.borderBottomRightRadius = 3;
            _progressBarContainer.style.display = DisplayStyle.None;
            
            _progressBarFill = new VisualElement();
            _progressBarFill.name = "ProgressBarFill";
            _progressBarFill.style.height = Length.Percent(100);
            _progressBarFill.style.width = Length.Percent(0);
            _progressBarFill.style.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f);
            _progressBarFill.style.borderTopLeftRadius = 3;
            _progressBarFill.style.borderTopRightRadius = 3;
            _progressBarFill.style.borderBottomLeftRadius = 3;
            _progressBarFill.style.borderBottomRightRadius = 3;
            
            _progressBarContainer.Add(_progressBarFill);
            mainContainer.Add(_progressBarContainer);
            
            // Controls container
            _controlsContainer = new VisualElement();
            _controlsContainer.name = "ControlsContainer";
            _controlsContainer.style.flexDirection = FlexDirection.Row;
            _controlsContainer.style.justifyContent = Justify.Center;
            _controlsContainer.style.alignItems = Align.Center;
            _controlsContainer.style.flexWrap = Wrap.Wrap;
            
            // Create control hints
            _moveHint = CreateControlHint("Move");
            _lookHint = CreateControlHint("Look");
            _mask1Hint = CreateControlHint("ToggleMask1");
            _mask2Hint = CreateControlHint("ToggleMask2");
            _mask3Hint = CreateControlHint("ToggleMask3");
            _dropHint = CreateControlHint("DropMask");
            _restartHint = CreateControlHint("SelfDestruct");
            _pauseHint = CreateControlHint("Pause");
            
            _controlsContainer.Add(_moveHint);
            _controlsContainer.Add(CreateSeparator());
            _controlsContainer.Add(_lookHint);
            _controlsContainer.Add(CreateSeparator());
            _controlsContainer.Add(_mask1Hint);
            _controlsContainer.Add(_mask2Hint);
            _controlsContainer.Add(_mask3Hint);
            _controlsContainer.Add(CreateSeparator());
            _controlsContainer.Add(_dropHint);
            _controlsContainer.Add(CreateSeparator());
            _controlsContainer.Add(_restartHint);
            _controlsContainer.Add(CreateSeparator());
            _controlsContainer.Add(_pauseHint);
            
            mainContainer.Add(_controlsContainer);
            _root.Add(mainContainer);
        }

        private VisualElement CreateControlHint(string actionName)
        {
            var container = new VisualElement();
            container.name = $"Hint_{actionName}";
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginLeft = 8;
            container.style.marginRight = 8;
            
            var keyLabel = new Label();
            keyLabel.name = "Key";
            keyLabel.AddToClassList("control-key");
            keyLabel.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            keyLabel.style.color = Color.white;
            keyLabel.style.paddingLeft = 6;
            keyLabel.style.paddingRight = 6;
            keyLabel.style.paddingTop = 2;
            keyLabel.style.paddingBottom = 2;
            keyLabel.style.borderTopLeftRadius = 4;
            keyLabel.style.borderTopRightRadius = 4;
            keyLabel.style.borderBottomLeftRadius = 4;
            keyLabel.style.borderBottomRightRadius = 4;
            keyLabel.style.fontSize = 12;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.marginRight = 4;
            
            var actionLabel = new Label();
            actionLabel.name = "Action";
            actionLabel.AddToClassList("control-action");
            actionLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            actionLabel.style.fontSize = 11;
            
            container.Add(keyLabel);
            container.Add(actionLabel);
            
            // Set initial values
            UpdateHintElement(container, actionName);
            
            return container;
        }

        private VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.width = 1;
            separator.style.height = 16;
            separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            separator.style.marginLeft = 4;
            separator.style.marginRight = 4;
            return separator;
        }

        private void UpdateHintElement(VisualElement container, string actionName)
        {
            var keyLabel = container.Q<Label>("Key");
            var actionLabel = container.Q<Label>("Action");
            
            if (keyLabel != null)
            {
                keyLabel.text = InputIconProvider.GetBindingText(actionName);
            }
            
            if (actionLabel != null)
            {
                actionLabel.text = InputIconProvider.GetActionDisplayName(actionName);
            }
        }

        private void UpdateControlHints()
        {
            if (_moveHint != null) UpdateHintElement(_moveHint, "Move");
            if (_lookHint != null) UpdateHintElement(_lookHint, "Look");
            if (_mask1Hint != null) UpdateHintElement(_mask1Hint, "ToggleMask1");
            if (_mask2Hint != null) UpdateHintElement(_mask2Hint, "ToggleMask2");
            if (_mask3Hint != null) UpdateHintElement(_mask3Hint, "ToggleMask3");
            if (_dropHint != null) UpdateHintElement(_dropHint, "DropMask");
            if (_restartHint != null) UpdateHintElement(_restartHint, "SelfDestruct");
            if (_pauseHint != null) UpdateHintElement(_pauseHint, "Pause");
        }

        private void OnInputDeviceChanged(InputManager.InputDeviceType deviceType)
        {
            UpdateControlHints();
        }

        private void OnSelfDestructStarted()
        {
            _isShowingProgress = true;
            if (_progressBarContainer != null)
            {
                _progressBarContainer.style.display = DisplayStyle.Flex;
            }
        }

        private void OnSelfDestructProgress(float progress)
        {
            _currentProgress = progress;
            if (_progressBarFill != null)
            {
                _progressBarFill.style.width = Length.Percent(progress * 100);
                
                // Change color as it fills
                float r = 0.9f;
                float g = 0.2f + (1f - progress) * 0.3f;
                float b = 0.2f;
                _progressBarFill.style.backgroundColor = new Color(r, g, b, 1f);
            }
        }

        private void OnSelfDestructCancelled()
        {
            _isShowingProgress = false;
            _currentProgress = 0;
            if (_progressBarContainer != null)
            {
                _progressBarContainer.style.display = DisplayStyle.None;
            }
            if (_progressBarFill != null)
            {
                _progressBarFill.style.width = Length.Percent(0);
            }
        }

        private void OnSelfDestructTriggered()
        {
            _isShowingProgress = false;
            _currentProgress = 0;
            if (_progressBarContainer != null)
            {
                _progressBarContainer.style.display = DisplayStyle.None;
            }
            if (_progressBarFill != null)
            {
                _progressBarFill.style.width = Length.Percent(0);
            }
        }

        /// <summary>
        /// Shows the controls bar.
        /// </summary>
        public void Show()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// Hides the controls bar.
        /// </summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Toggles controls bar visibility.
        /// </summary>
        public void Toggle()
        {
            if (_root != null)
            {
                bool isVisible = _root.style.display != DisplayStyle.None;
                _root.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
    }
}
