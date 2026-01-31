using UnityEngine;
using UnityEngine.UIElements;
using BreakingHue.Core;

namespace BreakingHue.UI
{
    /// <summary>
    /// Controller for the end game scene UI.
    /// Displays completion message and provides return to main menu button.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EndGameController : MonoBehaviour
    {
        [Header("Fallback Settings")]
        [Tooltip("Fallback title if no config is provided")]
        [SerializeField] private string fallbackTitle = "Congratulations!";
        
        [TextArea(3, 10)]
        [Tooltip("Fallback message if no config is provided")]
        [SerializeField] private string fallbackMessage = "You have completed the game!\n\nThank you for playing!";
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        
        private UIDocument _uiDocument;
        private VisualElement _root;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _menuButton;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            CreateUI();
            
            // Get config from EndGameManager
            var config = EndGameManager.ActiveConfig;
            
            if (config != null)
            {
                SetContent(config.completionTitle, config.completionText);
                
                // Play completion sound if available
                if (audioSource != null && config.completionSound != null)
                {
                    audioSource.PlayOneShot(config.completionSound);
                }
                
                // Play background music if available
                if (audioSource != null && config.endScreenMusic != null)
                {
                    audioSource.clip = config.endScreenMusic;
                    audioSource.loop = true;
                    audioSource.Play();
                }
            }
            else
            {
                SetContent(fallbackTitle, fallbackMessage);
            }
        }

        private void CreateUI()
        {
            if (_uiDocument == null) return;
            
            _root = _uiDocument.rootVisualElement;
            if (_root == null)
            {
                _root = new VisualElement();
            }
            
            _root.Clear();
            
            // Full screen dark background
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = new StyleColor(new Color(0.04f, 0.05f, 0.08f, 1f));
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            
            // Main container
            var container = new VisualElement();
            container.name = "EndGameContainer";
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.Center;
            container.style.paddingTop = 60;
            container.style.paddingBottom = 60;
            container.style.paddingLeft = 80;
            container.style.paddingRight = 80;
            container.style.maxWidth = 700;
            container.style.backgroundColor = new StyleColor(new Color(0.06f, 0.07f, 0.1f, 0.95f));
            container.style.borderTopLeftRadius = 20;
            container.style.borderTopRightRadius = 20;
            container.style.borderBottomLeftRadius = 20;
            container.style.borderBottomRightRadius = 20;
            container.style.borderTopWidth = 3;
            container.style.borderBottomWidth = 3;
            container.style.borderLeftWidth = 3;
            container.style.borderRightWidth = 3;
            var borderColor = new Color(0f, 0.78f, 1f, 0.6f);
            container.style.borderTopColor = new StyleColor(borderColor);
            container.style.borderBottomColor = new StyleColor(borderColor);
            container.style.borderLeftColor = new StyleColor(borderColor);
            container.style.borderRightColor = new StyleColor(borderColor);
            
            // Title label
            _titleLabel = new Label();
            _titleLabel.name = "Title";
            _titleLabel.style.fontSize = 42;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new StyleColor(new Color(0f, 0.9f, 0.8f, 1f));
            _titleLabel.style.marginBottom = 40;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(_titleLabel);
            
            // Message label
            _messageLabel = new Label();
            _messageLabel.name = "Message";
            _messageLabel.style.fontSize = 20;
            _messageLabel.style.color = new StyleColor(new Color(0.85f, 0.88f, 0.95f, 0.95f));
            _messageLabel.style.marginBottom = 50;
            _messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _messageLabel.style.whiteSpace = WhiteSpace.Normal;
            container.Add(_messageLabel);
            
            // Main menu button
            _menuButton = new Button();
            _menuButton.name = "MenuButton";
            _menuButton.text = "Return to Main Menu";
            _menuButton.style.paddingTop = 16;
            _menuButton.style.paddingBottom = 16;
            _menuButton.style.paddingLeft = 40;
            _menuButton.style.paddingRight = 40;
            _menuButton.style.fontSize = 20;
            _menuButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _menuButton.style.color = new StyleColor(Color.white);
            _menuButton.style.backgroundColor = new StyleColor(new Color(0f, 0.7f, 0.86f, 0.9f));
            _menuButton.style.borderTopLeftRadius = 12;
            _menuButton.style.borderTopRightRadius = 12;
            _menuButton.style.borderBottomLeftRadius = 12;
            _menuButton.style.borderBottomRightRadius = 12;
            _menuButton.style.borderTopWidth = 2;
            _menuButton.style.borderBottomWidth = 4;
            _menuButton.style.borderLeftWidth = 2;
            _menuButton.style.borderRightWidth = 2;
            var buttonBorderColor = new Color(0f, 0.55f, 0.7f, 1f);
            _menuButton.style.borderTopColor = new StyleColor(buttonBorderColor);
            _menuButton.style.borderBottomColor = new StyleColor(buttonBorderColor);
            _menuButton.style.borderLeftColor = new StyleColor(buttonBorderColor);
            _menuButton.style.borderRightColor = new StyleColor(buttonBorderColor);
            
            _menuButton.RegisterCallback<ClickEvent>(OnMenuButtonClicked);
            _menuButton.RegisterCallback<MouseEnterEvent>(evt => {
                _menuButton.style.backgroundColor = new StyleColor(new Color(0f, 0.8f, 0.95f, 0.95f));
            });
            _menuButton.RegisterCallback<MouseLeaveEvent>(evt => {
                _menuButton.style.backgroundColor = new StyleColor(new Color(0f, 0.7f, 0.86f, 0.9f));
            });
            
            container.Add(_menuButton);
            
            _root.Add(container);
        }

        private void SetContent(string title, string message)
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = title;
            }
            
            if (_messageLabel != null)
            {
                _messageLabel.text = message;
            }
        }

        private void OnMenuButtonClicked(ClickEvent evt)
        {
            EndGameManager.ReturnToMainMenu();
        }

        /// <summary>
        /// Force set the content (for testing).
        /// </summary>
        public void SetContentManual(string title, string message)
        {
            SetContent(title, message);
        }
    }
}
