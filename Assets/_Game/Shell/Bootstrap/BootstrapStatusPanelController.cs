using Ggj2026.Game.Shell.Diagnostics;
using Ggj2026.Game.Shared.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Ggj2026.Game.Shared.Telemetry;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ggj2026.Game.Shell.Bootstrap
{
    [RequireComponent(typeof(UIDocument))]
    public class BootstrapStatusPanelController : MonoBehaviour
    {
        [SerializeField] private string title = "Preparing Persistent World";
        [SerializeField] private string status = "Loading startup scene flow...";
        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private PanelSettings panelSettings;

        private UIDocument _uiDocument;

        private void Awake()
        {
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

            root.Q<Label>("BootstrapTitleLabel").text = title;
            root.Q<Label>("BootstrapStatusLabel").text = status;

            SessionTelemetryService.Active?.RecordUiAction(
                SessionActionKind.PanelOpened,
                SessionActorKind.System,
                new SessionUiSource
                {
                    panelId = "BootstrapStatus",
                    elementId = "BootstrapStatusRoot",
                    controller = nameof(BootstrapStatusPanelController)
                },
                "{\"panel\":\"BootstrapStatus\"}",
                "Bootstrap status panel opened.");
        }

        private void BuildFallbackUi(VisualElement root)
        {
            var shell = new VisualElement();
            shell.style.flexGrow = 1f;
            shell.style.justifyContent = Justify.Center;
            shell.style.alignItems = Align.Center;

            var card = new VisualElement();
            card.style.width = 420f;
            card.style.paddingLeft = 24f;
            card.style.paddingRight = 24f;
            card.style.paddingTop = 20f;
            card.style.paddingBottom = 20f;
            card.style.backgroundColor = new Color(0.12f, 0.16f, 0.21f, 0.96f);
            card.style.borderTopLeftRadius = 12f;
            card.style.borderTopRightRadius = 12f;
            card.style.borderBottomLeftRadius = 12f;
            card.style.borderBottomRightRadius = 12f;

            var titleLabel = new Label { name = "BootstrapTitleLabel" };
            titleLabel.style.fontSize = 22f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var statusLabel = new Label { name = "BootstrapStatusLabel" };
            statusLabel.style.marginTop = 8f;

            card.Add(titleLabel);
            card.Add(statusLabel);
            shell.Add(card);
            root.Add(shell);
        }
    }
}
