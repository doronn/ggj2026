using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.Diagnostics;
using Ggj2026.Game.Shell.Navigation;
using Ggj2026.Game.Shared.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Ggj2026.Game.Shell.Bootstrap
{
    [RequireComponent(typeof(UIDocument))]
    public class PersistentMainMenuController : MonoBehaviour
    {
        [SerializeField] private RuntimeGameConfig runtimeGameConfig;
        [SerializeField] private string title = "Persistent Hex-World";
        [SerializeField] private string subtitle = "4X / RTS Foundation";
        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private PanelSettings panelSettings;

        private UIDocument _uiDocument;
        private SceneNavigator _sceneNavigator;

        public void Construct([InjectOptional] SceneNavigator sceneNavigator)
        {
            _sceneNavigator = sceneNavigator;
        }

        private void Awake()
        {
            _sceneNavigator ??= new SceneNavigator();
            _uiDocument = GetComponent<UIDocument>();
            EnsureDocumentConfiguration();
            BuildUi();
        }

        private void EnsureDocumentConfiguration()
        {
            if (_uiDocument == null)
            {
                _uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (panelSettings == null)
            {
                panelSettings = LoadDefaultPanelSettings();
            }

            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.hideFlags = HideFlags.HideAndDontSave;
            }

            _uiDocument.panelSettings = panelSettings;
        }

        private static PanelSettings LoadDefaultPanelSettings()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPathConstants.RuntimePanelSettings);
#else
            return null;
#endif
        }

        private void BuildUi()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(root);
            }
            else
            {
                BuildFallbackUi(root);
            }

            root.Q<Label>("TitleLabel").text = title;
            root.Q<Label>("SubtitleLabel").text = subtitle;
            root.Q<Button>("EnterLocalWorldButton").clicked += () => LoadClientWorld().Forget();
            root.Q<Button>("OpenToolsSceneButton").clicked += () => LoadScene(runtimeGameConfig != null ? runtimeGameConfig.ToolsSceneName : SceneCatalog.Tools).Forget();
            root.Q<Button>("QuitButton").clicked += Quit;
        }

        private void BuildFallbackUi(VisualElement root)
        {
            var shell = new VisualElement { name = "MainMenuRoot" };
            shell.style.flexGrow = 1f;
            shell.style.alignItems = Align.Center;
            shell.style.justifyContent = Justify.Center;
            shell.style.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 0.94f);

            var card = new VisualElement { name = "MainMenuCard" };
            card.style.width = 460f;
            card.style.paddingLeft = 28f;
            card.style.paddingRight = 28f;
            card.style.paddingTop = 24f;
            card.style.paddingBottom = 24f;
            card.style.backgroundColor = new Color(0.16f, 0.18f, 0.22f, 0.98f);
            card.style.borderBottomLeftRadius = 12f;
            card.style.borderBottomRightRadius = 12f;
            card.style.borderTopLeftRadius = 12f;
            card.style.borderTopRightRadius = 12f;
            card.style.flexDirection = FlexDirection.Column;

            var titleLabel = new Label { name = "TitleLabel" };
            titleLabel.style.fontSize = 26f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.95f, 0.96f, 0.98f);

            var subtitleLabel = new Label { name = "SubtitleLabel" };
            subtitleLabel.style.fontSize = 14f;
            subtitleLabel.style.color = new Color(0.74f, 0.80f, 0.84f);
            subtitleLabel.style.marginTop = 10f;

            card.Add(titleLabel);
            card.Add(subtitleLabel);
            card.Add(CreateMenuButton("EnterLocalWorldButton", "Enter Local World"));
            card.Add(CreateMenuButton("OpenToolsSceneButton", "Open Tools Scene"));
            card.Add(CreateMenuButton("QuitButton", "Quit"));

            shell.Add(card);
            root.Add(shell);
        }

        private static Button CreateMenuButton(string name, string text)
        {
            var button = new Button { name = name, text = text };
            button.style.height = 38f;
            return button;
        }

        private UniTaskVoid LoadClientWorld()
        {
            SessionTelemetryService.Active?.RecordUiAction(
                SessionActionKind.MenuAction,
                SessionActorKind.Player,
                new SessionUiSource { panelId = "PersistentMainMenu", elementId = "EnterLocalWorldButton", controller = nameof(PersistentMainMenuController) },
                "{\"action\":\"EnterLocalWorld\"}",
                "Main menu action selected.");
            string sceneName = runtimeGameConfig != null ? runtimeGameConfig.ClientWorldSceneName : SceneCatalog.ClientWorld;
            return LoadScene(sceneName);
        }

        private async UniTaskVoid LoadScene(string sceneName)
        {
            await _sceneNavigator.LoadSingleAsync(sceneName);
        }

        private void Quit()
        {
            SessionTelemetryService.Active?.RecordUiAction(
                SessionActionKind.MenuAction,
                SessionActorKind.Player,
                new SessionUiSource { panelId = "PersistentMainMenu", elementId = "QuitButton", controller = nameof(PersistentMainMenuController) },
                "{\"action\":\"Quit\"}",
                "Main menu quit selected.");
            Application.Quit();
        }
    }
}
