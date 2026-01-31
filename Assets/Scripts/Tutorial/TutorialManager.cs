using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Gameplay;
using BreakingHue.UI;
using BreakingHue.Input;

namespace BreakingHue.Tutorial
{
    /// <summary>
    /// Manages the tutorial system for Breaking Hue.
    /// Triggers tutorials based on game events and tracks completion state.
    /// Integrates with the save system to persist tutorial progress.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private bool enableTutorials = true;
        [SerializeField] private float tutorialCooldown = 2f;
        
        private TutorialSaveData _saveData = new TutorialSaveData();
        private Dictionary<string, TutorialStep> _tutorials;
        private TutorialPromptUI _promptUI;
        private MaskInventory _inventory;
        
        private string _currentTutorialId;
        private float _lastTutorialTime;
        private bool _tutorialActive;
        
        /// <summary>
        /// Event fired when a tutorial is triggered.
        /// </summary>
        public static event Action<string, TutorialStep> OnTutorialTriggered;
        
        /// <summary>
        /// Event fired when a tutorial is completed/dismissed.
        /// </summary>
        public static event Action<string> OnTutorialCompleted;
        
        /// <summary>
        /// Gets the current tutorial save data.
        /// </summary>
        public TutorialSaveData SaveData => _saveData;

        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _tutorials = TutorialDefinitions.GetAllTutorials();
        }

        private void Start()
        {
            Debug.Log("[TutorialManager] Starting...");
            
            // Find or create the tutorial UI
            _promptUI = FindObjectOfType<TutorialPromptUI>();
            if (_promptUI == null)
            {
                Debug.Log("[TutorialManager] TutorialPromptUI not found, creating...");
                var uiGO = new GameObject("TutorialPromptUI");
                _promptUI = uiGO.AddComponent<TutorialPromptUI>();
                
                // Also need to add UIDocument for the prompt to work
                var uiDoc = uiGO.GetComponent<UIDocument>();
                if (uiDoc == null)
                {
                    uiDoc = uiGO.AddComponent<UIDocument>();
                    // Try to find and assign PanelSettings
                    var existingDoc = FindObjectOfType<UIDocument>();
                    if (existingDoc != null && existingDoc.panelSettings != null)
                    {
                        uiDoc.panelSettings = existingDoc.panelSettings;
                    }
                }
            }
            else
            {
                Debug.Log("[TutorialManager] Found existing TutorialPromptUI");
            }
            
            // Try to get inventory via Zenject if not already injected
            if (_inventory == null)
            {
                Debug.Log("[TutorialManager] Inventory not injected, trying to find via SceneContext...");
                var sceneContext = FindObjectOfType<Zenject.SceneContext>();
                if (sceneContext != null && sceneContext.Container != null)
                {
                    _inventory = sceneContext.Container.TryResolve<MaskInventory>();
                    if (_inventory != null)
                    {
                        Debug.Log("[TutorialManager] Found inventory via SceneContext");
                    }
                }
            }
            
            // Subscribe to game events
            SubscribeToEvents();
            
            Debug.Log($"[TutorialManager] Initialized. Tutorials enabled: {enableTutorials}, Inventory: {(_inventory != null ? "Found" : "NULL")}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            Debug.Log("[TutorialManager] Subscribing to events...");
            
            // Barrier events
            ColorBarrier.OnPlayerBlockedByBarrier += OnPlayerBlockedByBarrier;
            Debug.Log("[TutorialManager] Subscribed to ColorBarrier.OnPlayerBlockedByBarrier");
            
            // Inventory events
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += OnInventoryChanged;
                _inventory.OnMaskDropped += OnMaskDropped;
                Debug.Log("[TutorialManager] Subscribed to inventory events");
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Inventory is null, cannot subscribe to inventory events");
            }
            
            // Pickup events
            MaskPickup.OnMaskPickedUp += OnMaskPickedUp;
            MaskPickup.OnInventoryFullAttempt += OnInventoryFullAttempt;
            Debug.Log("[TutorialManager] Subscribed to MaskPickup events");
        }

        private void UnsubscribeFromEvents()
        {
            ColorBarrier.OnPlayerBlockedByBarrier -= OnPlayerBlockedByBarrier;
            
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= OnInventoryChanged;
                _inventory.OnMaskDropped -= OnMaskDropped;
            }
            
            MaskPickup.OnMaskPickedUp -= OnMaskPickedUp;
            MaskPickup.OnInventoryFullAttempt -= OnInventoryFullAttempt;
        }

        /// <summary>
        /// Triggers a tutorial by ID if it hasn't been completed.
        /// </summary>
        public bool TriggerTutorial(string tutorialId)
        {
            if (!enableTutorials) return false;
            if (_saveData.IsCompleted(tutorialId)) return false;
            if (_tutorialActive) return false;
            if (Time.time - _lastTutorialTime < tutorialCooldown) return false;
            
            if (!_tutorials.TryGetValue(tutorialId, out var tutorial))
            {
                Debug.LogWarning($"[TutorialManager] Unknown tutorial ID: {tutorialId}");
                return false;
            }
            
            _currentTutorialId = tutorialId;
            _tutorialActive = true;
            _lastTutorialTime = Time.time;
            
            // Show the tutorial
            if (_promptUI != null)
            {
                _promptUI.ShowTutorial(tutorial);
            }
            
            OnTutorialTriggered?.Invoke(tutorialId, tutorial);
            
            Debug.Log($"[TutorialManager] Tutorial triggered: {tutorialId}");
            
            // Auto-dismiss timed tutorials
            if (tutorial.displayDuration > 0)
            {
                Invoke(nameof(DismissCurrentTutorial), tutorial.displayDuration);
            }
            
            return true;
        }

        /// <summary>
        /// Completes (permanently dismisses) the current tutorial.
        /// </summary>
        public void CompleteTutorial(string tutorialId)
        {
            if (string.IsNullOrEmpty(tutorialId)) return;
            
            _saveData.MarkCompleted(tutorialId);
            
            if (_currentTutorialId == tutorialId)
            {
                DismissCurrentTutorial();
            }
            
            OnTutorialCompleted?.Invoke(tutorialId);
            
            Debug.Log($"[TutorialManager] Tutorial completed: {tutorialId}");
        }

        /// <summary>
        /// Dismisses the current tutorial without marking it as completed.
        /// </summary>
        public void DismissCurrentTutorial()
        {
            if (!_tutorialActive) return;
            
            if (_promptUI != null)
            {
                _promptUI.HideTutorial();
            }
            
            // If this was a timed tutorial, mark it complete
            if (_tutorials.TryGetValue(_currentTutorialId, out var tutorial) && tutorial.displayDuration > 0)
            {
                _saveData.MarkCompleted(_currentTutorialId);
            }
            
            _currentTutorialId = null;
            _tutorialActive = false;
        }

        /// <summary>
        /// Loads tutorial state from save data.
        /// </summary>
        public void LoadState(TutorialSaveData data)
        {
            if (data != null)
            {
                _saveData = data.Clone();
            }
            else
            {
                _saveData = new TutorialSaveData();
            }
        }

        /// <summary>
        /// Gets a copy of the current tutorial state for saving.
        /// </summary>
        public TutorialSaveData GetStateForSave()
        {
            return _saveData.Clone();
        }

        /// <summary>
        /// Resets all tutorial progress.
        /// </summary>
        public void ResetProgress()
        {
            _saveData.Reset();
            Debug.Log("[TutorialManager] Tutorial progress reset");
        }

        /// <summary>
        /// Checks if a tutorial has been completed.
        /// </summary>
        public bool IsTutorialCompleted(string tutorialId)
        {
            return _saveData.IsCompleted(tutorialId);
        }

        #region Event Handlers

        private void OnPlayerBlockedByBarrier(ColorBarrier barrier, ColorType requiredColor)
        {
            if (_inventory == null) return;
            
            // Check if player has any masks at all
            bool hasAnyMask = false;
            bool hasMatchingInactiveMask = false;
            ColorType inactiveMasks = ColorType.None;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                var color = _inventory.GetSlot(i);
                if (color != ColorType.None)
                {
                    hasAnyMask = true;
                    if (!_inventory.IsSlotActive(i))
                    {
                        inactiveMasks |= color;
                    }
                }
            }
            
            // Check if inactive masks could pass through
            hasMatchingInactiveMask = ColorTypeExtensions.CanPassThrough(inactiveMasks, requiredColor);
            
            // Determine which tutorial to show
            if (hasAnyMask && hasMatchingInactiveMask)
            {
                // Player has matching mask but hasn't equipped it
                TriggerTutorial(TutorialDefinitions.TUTORIAL_EQUIP_MASK);
            }
            else if (hasAnyMask)
            {
                // Check if this is a combined color barrier
                bool needsCombined = requiredColor.HasFlag(ColorType.Red | ColorType.Yellow) ||
                                     requiredColor.HasFlag(ColorType.Red | ColorType.Blue) ||
                                     requiredColor.HasFlag(ColorType.Yellow | ColorType.Blue) ||
                                     requiredColor == ColorType.Black;
                
                if (needsCombined)
                {
                    TriggerTutorial(TutorialDefinitions.TUTORIAL_COMBINE_MASKS);
                }
            }
        }

        private void OnMaskPickedUp(MaskPickup pickup, ColorType color)
        {
            // First mask pickup tutorial
            TriggerTutorial(TutorialDefinitions.TUTORIAL_PICKUP_MASK);
            
            // Complete the equip mask tutorial if it was showing (player figured it out)
            if (_currentTutorialId == TutorialDefinitions.TUTORIAL_EQUIP_MASK)
            {
                CompleteTutorial(TutorialDefinitions.TUTORIAL_EQUIP_MASK);
            }
        }

        private void OnInventoryChanged()
        {
            // Check if equip tutorial should be completed
            if (_currentTutorialId == TutorialDefinitions.TUTORIAL_EQUIP_MASK && _inventory != null)
            {
                bool hasActiveMask = false;
                for (int i = 0; i < MaskInventory.MaxSlots; i++)
                {
                    if (_inventory.IsSlotActive(i) && _inventory.GetSlot(i) != ColorType.None)
                    {
                        hasActiveMask = true;
                        break;
                    }
                }
                
                if (hasActiveMask)
                {
                    CompleteTutorial(TutorialDefinitions.TUTORIAL_EQUIP_MASK);
                }
            }
            
            // Check if combine tutorial should be completed
            if (_currentTutorialId == TutorialDefinitions.TUTORIAL_COMBINE_MASKS && _inventory != null)
            {
                int activeCount = 0;
                for (int i = 0; i < MaskInventory.MaxSlots; i++)
                {
                    if (_inventory.IsSlotActive(i) && _inventory.GetSlot(i) != ColorType.None)
                    {
                        activeCount++;
                    }
                }
                
                if (activeCount >= 2)
                {
                    CompleteTutorial(TutorialDefinitions.TUTORIAL_COMBINE_MASKS);
                }
            }
        }

        private void OnMaskDropped(int slotIndex, ColorType color)
        {
            // Complete drop tutorial if it was showing
            if (_currentTutorialId == TutorialDefinitions.TUTORIAL_DROP_MASK)
            {
                CompleteTutorial(TutorialDefinitions.TUTORIAL_DROP_MASK);
            }
        }

        private void OnInventoryFullAttempt(MaskPickup pickup)
        {
            // Trigger drop tutorial when player tries to pick up with full inventory
            TriggerTutorial(TutorialDefinitions.TUTORIAL_DROP_MASK);
        }

        #endregion
    }
}
