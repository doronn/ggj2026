using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Input;
using BreakingHue.Gameplay;

namespace BreakingHue.UI
{
    /// <summary>
    /// Manages contextual control prompts that appear based on game state.
    /// Adapts to input device (keyboard/mouse vs controller).
    /// Shows prompts only when actions are available/relevant.
    /// </summary>
    public class ContextualPromptController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        
        [Header("Settings")]
        [SerializeField] private float promptFadeDuration = 0.25f;
        [SerializeField] private float promptShowDelay = 0.5f;
        
        private VisualElement _root;
        private VisualElement _promptContainer;
        private Dictionary<string, ContextualPrompt> _activePrompts = new Dictionary<string, ContextualPrompt>();
        
        private MaskInventory _inventory;
        private PlayerController _playerController;
        
        // Prompt IDs
        public const string PROMPT_DROP_MASK = "drop_mask";
        public const string PROMPT_EQUIP_MASK = "equip_mask";
        public const string PROMPT_COMBINE_MASKS = "combine_masks";
        public const string PROMPT_PICKUP_MASK = "pickup_mask";
        
        // Track state for showing contextual prompts
        private bool _playerNearBarrier;
        private ColorType _nearbyBarrierColor;
        private bool _playerNearPickup;
        
        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            Debug.Log("[ContextualPromptController] Starting...");
            
            SetupUI();
            
            // Try to get inventory via Zenject if not already injected
            if (_inventory == null)
            {
                Debug.Log("[ContextualPromptController] Inventory not injected, trying to find via SceneContext...");
                var sceneContext = FindObjectOfType<Zenject.SceneContext>();
                if (sceneContext != null && sceneContext.Container != null)
                {
                    _inventory = sceneContext.Container.TryResolve<MaskInventory>();
                    if (_inventory != null)
                    {
                        Debug.Log("[ContextualPromptController] Found inventory via SceneContext");
                    }
                }
            }
            
            // Subscribe to events
            InputManager.OnInputDeviceChanged += OnInputDeviceChanged;
            
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += OnInventoryChanged;
                _inventory.OnMaskToggled += OnMaskToggled;
                Debug.Log("[ContextualPromptController] Subscribed to inventory events");
            }
            else
            {
                Debug.LogWarning("[ContextualPromptController] Inventory is null, cannot subscribe to inventory events");
            }
            
            // Find player controller
            _playerController = FindObjectOfType<PlayerController>();
            
            // Subscribe to barrier proximity events
            ColorBarrier.OnPlayerNearBarrier += OnPlayerNearBarrier;
            ColorBarrier.OnPlayerLeftBarrier += OnPlayerLeftBarrier;
            
            Debug.Log("[ContextualPromptController] Initialized");
        }

        private void OnDestroy()
        {
            InputManager.OnInputDeviceChanged -= OnInputDeviceChanged;
            
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= OnInventoryChanged;
                _inventory.OnMaskToggled -= OnMaskToggled;
            }
            
            ColorBarrier.OnPlayerNearBarrier -= OnPlayerNearBarrier;
            ColorBarrier.OnPlayerLeftBarrier -= OnPlayerLeftBarrier;
        }

        private void SetupUI()
        {
            if (uiDocument == null)
            {
                Debug.LogWarning("[ContextualPromptController] UIDocument is null, cannot setup UI");
                return;
            }
            
            // Ensure we have PanelSettings
            if (uiDocument.panelSettings == null)
            {
                Debug.LogWarning("[ContextualPromptController] UIDocument has no PanelSettings, trying to find one...");
                // Try to find PanelSettings from another UIDocument
                var allDocs = FindObjectsOfType<UIDocument>();
                foreach (var doc in allDocs)
                {
                    if (doc != uiDocument && doc.panelSettings != null)
                    {
                        uiDocument.panelSettings = doc.panelSettings;
                        Debug.Log("[ContextualPromptController] Borrowed PanelSettings from another UIDocument");
                        break;
                    }
                }
                
                if (uiDocument.panelSettings == null)
                {
                    Debug.LogError("[ContextualPromptController] No PanelSettings found! Contextual prompts will not work.");
                    return;
                }
            }
            
            _root = uiDocument.rootVisualElement;
            
            if (_root == null)
            {
                Debug.LogWarning("[ContextualPromptController] rootVisualElement is null, scheduling delayed setup...");
                StartCoroutine(SetupUIDelayed());
                return;
            }
            
            SetupUIInternal();
        }
        
        private System.Collections.IEnumerator SetupUIDelayed()
        {
            yield return null; // Wait one frame
            
            _root = uiDocument.rootVisualElement;
            if (_root != null)
            {
                SetupUIInternal();
            }
            else
            {
                Debug.LogError("[ContextualPromptController] rootVisualElement still null after delay");
            }
        }
        
        private void SetupUIInternal()
        {
            // Create prompt container
            _promptContainer = new VisualElement();
            _promptContainer.name = "ContextualPromptContainer";
            _promptContainer.style.position = Position.Absolute;
            _promptContainer.style.top = 0;
            _promptContainer.style.left = 0;
            _promptContainer.style.right = 0;
            _promptContainer.style.bottom = 0;
            _promptContainer.pickingMode = PickingMode.Ignore;
            
            _root.Add(_promptContainer);
            
            Debug.Log("[ContextualPromptController] UI setup complete");
        }

        private void OnInputDeviceChanged(InputManager.InputDeviceType deviceType)
        {
            // Update all active prompts with new key bindings
            foreach (var prompt in _activePrompts.Values)
            {
                UpdatePromptKey(prompt);
            }
        }

        private void OnInventoryChanged()
        {
            UpdateContextualPrompts();
        }

        private void OnMaskToggled(int slotIndex, bool isActive)
        {
            UpdateContextualPrompts();
        }

        private void OnPlayerNearBarrier(ColorBarrier barrier)
        {
            _playerNearBarrier = true;
            _nearbyBarrierColor = barrier.RequiredColor;
            UpdateContextualPrompts();
        }

        private void OnPlayerLeftBarrier()
        {
            _playerNearBarrier = false;
            _nearbyBarrierColor = ColorType.None;
            UpdateContextualPrompts();
        }

        private void UpdateContextualPrompts()
        {
            if (_inventory == null) return;
            
            // Check if player has any masks
            bool hasAnyMask = false;
            bool hasActiveMask = false;
            ColorType inactiveMaskColors = ColorType.None;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                var color = _inventory.GetSlot(i);
                if (color != ColorType.None)
                {
                    hasAnyMask = true;
                    if (_inventory.IsSlotActive(i))
                    {
                        hasActiveMask = true;
                    }
                    else
                    {
                        inactiveMaskColors |= color;
                    }
                }
            }
            
            // Equip mask prompt - show when near barrier and have matching inactive mask
            if (_playerNearBarrier && hasAnyMask && !hasActiveMask)
            {
                // Check if any inactive mask matches the barrier
                bool canPassWithInactive = ColorTypeExtensions.CanPassThrough(inactiveMaskColors, _nearbyBarrierColor);
                if (canPassWithInactive)
                {
                    ShowPrompt(PROMPT_EQUIP_MASK, "ToggleMask1", "Equip Mask", PromptPosition.Center);
                }
                else
                {
                    HidePrompt(PROMPT_EQUIP_MASK);
                }
            }
            else
            {
                HidePrompt(PROMPT_EQUIP_MASK);
            }
            
            // Combine masks prompt - show when near barrier requiring combined color
            if (_playerNearBarrier && hasAnyMask)
            {
                // Check if barrier requires combined colors (secondary or black)
                bool needsCombined = _nearbyBarrierColor.HasFlag(ColorType.Red | ColorType.Yellow) ||
                                     _nearbyBarrierColor.HasFlag(ColorType.Red | ColorType.Blue) ||
                                     _nearbyBarrierColor.HasFlag(ColorType.Yellow | ColorType.Blue) ||
                                     _nearbyBarrierColor == ColorType.Black;
                
                ColorType combined = _inventory.GetCombinedActiveColor();
                bool canPassNow = ColorTypeExtensions.CanPassThrough(combined, _nearbyBarrierColor);
                
                if (needsCombined && !canPassNow)
                {
                    ShowPrompt(PROMPT_COMBINE_MASKS, "", "Equip Multiple Masks", PromptPosition.Center);
                }
                else
                {
                    HidePrompt(PROMPT_COMBINE_MASKS);
                }
            }
            else
            {
                HidePrompt(PROMPT_COMBINE_MASKS);
            }
        }

        /// <summary>
        /// Shows a contextual prompt.
        /// </summary>
        public void ShowPrompt(string promptId, string actionName, string displayText, PromptPosition position)
        {
            if (_activePrompts.ContainsKey(promptId))
            {
                // Update existing prompt
                var existing = _activePrompts[promptId];
                existing.ActionName = actionName;
                UpdatePromptKey(existing);
                existing.ActionLabel.text = displayText;
                return;
            }
            
            // Create new prompt
            var prompt = CreatePromptElement(promptId, actionName, displayText, position);
            _activePrompts[promptId] = prompt;
            _promptContainer.Add(prompt.Container);
            
            // Fade in
            prompt.Container.style.opacity = 0;
            prompt.Container.schedule.Execute(() => {
                prompt.Container.style.opacity = 1;
            }).ExecuteLater((long)(promptShowDelay * 1000));
        }

        /// <summary>
        /// Hides a contextual prompt.
        /// </summary>
        public void HidePrompt(string promptId)
        {
            if (!_activePrompts.TryGetValue(promptId, out var prompt))
                return;
            
            _activePrompts.Remove(promptId);
            
            // Fade out and remove
            prompt.Container.style.opacity = 0;
            prompt.Container.schedule.Execute(() => {
                prompt.Container.RemoveFromHierarchy();
            }).ExecuteLater((long)(promptFadeDuration * 1000));
        }

        /// <summary>
        /// Hides all contextual prompts.
        /// </summary>
        public void HideAllPrompts()
        {
            var promptIds = new List<string>(_activePrompts.Keys);
            foreach (var id in promptIds)
            {
                HidePrompt(id);
            }
        }

        private ContextualPrompt CreatePromptElement(string promptId, string actionName, string displayText, PromptPosition position)
        {
            var container = new VisualElement();
            container.name = $"Prompt_{promptId}";
            container.AddToClassList("contextual-prompt");
            container.style.position = Position.Absolute;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 10;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.backgroundColor = new StyleColor(new Color(0.06f, 0.07f, 0.1f, 0.92f));
            container.style.borderTopLeftRadius = 12;
            container.style.borderTopRightRadius = 12;
            container.style.borderBottomLeftRadius = 12;
            container.style.borderBottomRightRadius = 12;
            container.style.borderTopWidth = 2;
            container.style.borderBottomWidth = 2;
            container.style.borderLeftWidth = 2;
            container.style.borderRightWidth = 2;
            var borderColor = new Color(0f, 0.78f, 1f, 0.5f);
            container.style.borderTopColor = new StyleColor(borderColor);
            container.style.borderBottomColor = new StyleColor(borderColor);
            container.style.borderLeftColor = new StyleColor(borderColor);
            container.style.borderRightColor = new StyleColor(borderColor);
            container.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            container.style.transitionDuration = new List<TimeValue> { new TimeValue(promptFadeDuration, TimeUnit.Second) };
            
            // Position the prompt
            ApplyPromptPosition(container, position);
            
            // Key label (if action specified) - uses device-specific styling
            Label keyLabel = null;
            if (!string.IsNullOrEmpty(actionName))
            {
                // Create styled button using InputIconProvider
                var styledButton = InputIconProvider.CreateStyledButton(actionName, 18f);
                styledButton.name = "Key";
                styledButton.style.marginRight = 12;
                container.Add(styledButton);
                
                // Cast to Label for storage (CreateStyledButton returns a Label)
                keyLabel = styledButton as Label;
            }
            
            // Action label
            var actionLabel = new Label(displayText);
            actionLabel.name = "Action";
            actionLabel.style.fontSize = 16;
            actionLabel.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.9f, 0.95f));
            container.Add(actionLabel);
            
            return new ContextualPrompt
            {
                Id = promptId,
                ActionName = actionName,
                Container = container,
                KeyLabel = keyLabel,
                ActionLabel = actionLabel
            };
        }

        private void ApplyPromptPosition(VisualElement element, PromptPosition position)
        {
            switch (position)
            {
                case PromptPosition.TopCenter:
                    element.style.top = 100;
                    element.style.left = Length.Percent(50);
                    element.style.translate = new Translate(Length.Percent(-50), 0);
                    break;
                    
                case PromptPosition.Center:
                    element.style.top = Length.Percent(40);
                    element.style.left = Length.Percent(50);
                    element.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                    break;
                    
                case PromptPosition.BottomCenter:
                    element.style.bottom = 200;
                    element.style.left = Length.Percent(50);
                    element.style.translate = new Translate(Length.Percent(-50), 0);
                    break;
                    
                case PromptPosition.AboveHUD:
                    element.style.bottom = 220;
                    element.style.left = Length.Percent(50);
                    element.style.translate = new Translate(Length.Percent(-50), 0);
                    break;
            }
        }

        private void UpdatePromptKey(ContextualPrompt prompt)
        {
            if (prompt.KeyLabel != null && !string.IsNullOrEmpty(prompt.ActionName))
            {
                // Update text and device-specific styling
                InputIconProvider.UpdateStyledButton(prompt.KeyLabel, prompt.ActionName);
            }
        }

        /// <summary>
        /// Prompt data structure.
        /// </summary>
        private class ContextualPrompt
        {
            public string Id;
            public string ActionName;
            public VisualElement Container;
            public Label KeyLabel;
            public Label ActionLabel;
        }
    }

    /// <summary>
    /// Position options for contextual prompts.
    /// </summary>
    public enum PromptPosition
    {
        TopCenter,
        Center,
        BottomCenter,
        AboveHUD
    }
}
