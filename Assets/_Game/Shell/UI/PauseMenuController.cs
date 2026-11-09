using System;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shell.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ggj2026.Game.Shell.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private StyleSheet styleSheet;

        private VisualElement _overlay;
        private Button _resumeButton;
        private Button _exitButton;
        private float _previousTimeScale = 1f;
        private float _toggleCooldownEnd;
        private bool _isPaused;

        public static event Action OnGamePaused;
        public static event Action OnGameResumed;
        public static PauseMenuController Active { get; private set; }
        public bool IsPaused => _isPaused;

        private void Awake()
        {
            Active = this;
            EnsureDocument();
            BuildUi();
        }

        private void OnDestroy()
        {
            if (_isPaused)
            {
                Time.timeScale = _previousTimeScale;
            }

            if (Active == this)
            {
                Active = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Time.unscaledTime < _toggleCooldownEnd) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_isPaused)
                {
                    Resume();
                }
            }
        }

        public void TogglePause()
        {
            if (Time.unscaledTime < _toggleCooldownEnd) return;
            if (_isPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            _toggleCooldownEnd = Time.unscaledTime + 0.2f;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
            }

            _resumeButton?.Focus();
            OnGamePaused?.Invoke();
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            _toggleCooldownEnd = Time.unscaledTime + 0.2f;
            Time.timeScale = _previousTimeScale;

            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
            }

            OnGameResumed?.Invoke();
        }

        private void ExitToMenu()
        {
            if (_isPaused)
            {
                Time.timeScale = 1f;
                _isPaused = false;
            }

            new SceneNavigator().LoadSingleAsync(SceneCatalog.MainMenu).Forget();
        }

        private void EnsureDocument()
        {
            uiDocument ??= GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings == null)
            {
#if UNITY_EDITOR
                uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPathConstants.RuntimePanelSettings);
#endif
            }

            uiDocument.sortingOrder = 100;
        }

        private void BuildUi()
        {
            VisualElement root = uiDocument.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;

            if (styleSheet == null)
            {
#if UNITY_EDITOR
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Game/UI/Runtime/USS/PauseMenu.uss");
#endif
            }

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

#if UNITY_EDITOR
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Game/UI/Runtime/UXML/PauseMenu.uxml");
            if (tree != null)
            {
                tree.CloneTree(root);
            }
            else
#endif
            {
                BuildFallbackUi(root);
            }

            _overlay = root.Q<VisualElement>("PauseOverlay");
            _resumeButton = root.Q<Button>("ResumeButton");
            _exitButton = root.Q<Button>("ExitToMenuButton");

            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
            }

            if (_resumeButton != null)
            {
                _resumeButton.clicked += Resume;
                InteractiveButtonStyles.Apply(_resumeButton, new Color(40f / 255f, 130f / 255f, 75f / 255f, 0.96f));
            }

            if (_exitButton != null)
            {
                _exitButton.clicked += ExitToMenu;
                InteractiveButtonStyles.Apply(_exitButton, new Color(140f / 255f, 48f / 255f, 48f / 255f, 0.96f));
            }

            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_overlay);
        }

        private static void BuildFallbackUi(VisualElement root)
        {
            var overlay = new VisualElement { name = "PauseOverlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.right = 0;
            overlay.style.top = 0; overlay.style.bottom = 0;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.78f);
            overlay.style.display = DisplayStyle.None;

            var card = new VisualElement { name = "PauseCard" };
            card.style.width = 380;
            card.style.paddingLeft = 32; card.style.paddingRight = 32;
            card.style.paddingTop = 28; card.style.paddingBottom = 28;
            card.style.backgroundColor = new Color(16f / 255f, 22f / 255f, 28f / 255f, 0.98f);
            card.Add(new Label("Paused") { name = "PauseTitleLabel" });
            card.Add(new Label("Game is paused") { name = "PauseSubtitleLabel" });
            card.Add(new Button { name = "ResumeButton", text = "Resume" });
            card.Add(new Button { name = "ExitToMenuButton", text = "Exit to Menu" });
            overlay.Add(card);
            root.Add(overlay);
        }
    }
}
