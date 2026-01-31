using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace BreakingHue.UI
{
    /// <summary>
    /// UI overlay that fades to white and back for death/transition effects.
    /// Uses UI Toolkit for the overlay.
    /// </summary>
    public class WhiteOutEffect : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        
        [Header("Timing")]
        [SerializeField] private float fadeToWhiteDuration = 0.5f;
        [SerializeField] private float fadeFromWhiteDuration = 0.5f;
        [SerializeField] private float holdWhiteDuration = 0.2f;
        
        private VisualElement _overlay;
        private Coroutine _fadeCoroutine;
        private bool _isActive;
        
        /// <summary>
        /// Event fired when fade to white is complete (screen is fully white).
        /// </summary>
        public event Action OnFadeToWhiteComplete;
        
        /// <summary>
        /// Event fired when fade from white is complete (screen is clear).
        /// </summary>
        public event Action OnFadeFromWhiteComplete;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            SetupOverlay();
        }

        private void SetupOverlay()
        {
            if (uiDocument == null) return;
            
            var root = uiDocument.rootVisualElement;
            if (root == null) return;
            
            _overlay = root.Q<VisualElement>("WhiteOutOverlay");
            
            if (_overlay == null)
            {
                // Create overlay dynamically if not found in UXML
                _overlay = new VisualElement();
                _overlay.name = "WhiteOutOverlay";
                _overlay.style.position = Position.Absolute;
                _overlay.style.left = 0;
                _overlay.style.top = 0;
                _overlay.style.right = 0;
                _overlay.style.bottom = 0;
                _overlay.style.backgroundColor = new StyleColor(Color.white);
                _overlay.style.opacity = 0;
                _overlay.pickingMode = PickingMode.Ignore;
                root.Add(_overlay);
            }
            
            // Ensure overlay starts invisible
            _overlay.style.opacity = 0;
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Starts the full white-out sequence (fade to white, hold, fade back).
        /// </summary>
        public void PlayFullSequence(Action onMidPoint = null)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            
            _fadeCoroutine = StartCoroutine(FullSequenceCoroutine(onMidPoint));
        }

        /// <summary>
        /// Fades the screen to white.
        /// </summary>
        public void FadeToWhite(Action onComplete = null)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            
            _fadeCoroutine = StartCoroutine(FadeToWhiteCoroutine(onComplete));
        }

        /// <summary>
        /// Fades the screen from white back to clear.
        /// </summary>
        public void FadeFromWhite(Action onComplete = null)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            
            _fadeCoroutine = StartCoroutine(FadeFromWhiteCoroutine(onComplete));
        }

        private IEnumerator FullSequenceCoroutine(Action onMidPoint)
        {
            _isActive = true;
            
            // Fade to white
            yield return FadeToWhiteCoroutine(null);
            
            // Hold at white
            yield return new WaitForSeconds(holdWhiteDuration);
            
            // Fire mid-point callback (usually to trigger checkpoint restore)
            onMidPoint?.Invoke();
            OnFadeToWhiteComplete?.Invoke();
            
            // Small delay after restore
            yield return new WaitForSeconds(0.1f);
            
            // Fade back from white
            yield return FadeFromWhiteCoroutine(null);
            
            _isActive = false;
        }

        private IEnumerator FadeToWhiteCoroutine(Action onComplete)
        {
            if (_overlay == null) yield break;
            
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.pickingMode = PickingMode.Position; // Block input during fade
            
            float elapsed = 0f;
            float startOpacity = _overlay.resolvedStyle.opacity;
            
            while (elapsed < fadeToWhiteDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeToWhiteDuration);
                
                // Use ease-in curve for dramatic effect
                float easedT = t * t;
                
                _overlay.style.opacity = Mathf.Lerp(startOpacity, 1f, easedT);
                yield return null;
            }
            
            _overlay.style.opacity = 1f;
            onComplete?.Invoke();
        }

        private IEnumerator FadeFromWhiteCoroutine(Action onComplete)
        {
            if (_overlay == null) yield break;
            
            float elapsed = 0f;
            float startOpacity = _overlay.resolvedStyle.opacity;
            
            while (elapsed < fadeFromWhiteDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeFromWhiteDuration);
                
                // Use ease-out curve
                float easedT = 1f - (1f - t) * (1f - t);
                
                _overlay.style.opacity = Mathf.Lerp(startOpacity, 0f, easedT);
                yield return null;
            }
            
            _overlay.style.opacity = 0f;
            _overlay.style.display = DisplayStyle.None;
            _overlay.pickingMode = PickingMode.Ignore;
            
            onComplete?.Invoke();
            OnFadeFromWhiteComplete?.Invoke();
        }

        /// <summary>
        /// Immediately clears the overlay without animation.
        /// </summary>
        public void ClearOverlay()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            
            if (_overlay != null)
            {
                _overlay.style.opacity = 0f;
                _overlay.style.display = DisplayStyle.None;
                _overlay.pickingMode = PickingMode.Ignore;
            }
            
            _isActive = false;
        }

        /// <summary>
        /// Returns true if the white-out effect is currently active.
        /// </summary>
        public bool IsActive => _isActive;
        
        /// <summary>
        /// Duration for fade to white.
        /// </summary>
        public float FadeToWhiteDuration
        {
            get => fadeToWhiteDuration;
            set => fadeToWhiteDuration = value;
        }
        
        /// <summary>
        /// Duration for fade from white.
        /// </summary>
        public float FadeFromWhiteDuration
        {
            get => fadeFromWhiteDuration;
            set => fadeFromWhiteDuration = value;
        }
    }
}
