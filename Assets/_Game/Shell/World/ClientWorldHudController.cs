using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.Navigation;
using Ggj2026.Game.Shell.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ggj2026.Game.Shell.World
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClientWorldHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private PanelSettings panelSettings;

        private readonly HashSet<HexCoord> _strokeCells = new HashSet<HexCoord>();
        private readonly HashSet<HexEdgeCoord> _strokeEdges = new HashSet<HexEdgeCoord>();

        private RuntimeGameConfig _runtimeGameConfig;
        private LocalWorldDebugRenderer _renderer;
        private LocalColonyGameplayService _gameplayService;
        private RealTimeSimulationController _realTimeSimulationController;
        private SceneNavigator _sceneNavigator;
        private GameActionService _actionService;
        private GameQueryService _queryService;
        private OnboardingFlowService _onboardingFlowService;

        private VisualElement _root;
        private VisualElement _worldTopBar;
        private Label _worldNameLabel;
        private Label _tickLabel;
        private Label _simulationLabel;
        private Label _saveStateLabel;
        private Label _workerStatusLabel;
        private Label _growthStatusLabel;
        private Label _blockerStatusLabel;
        private Label _telemetryStatusLabel;
        private Label _overlayStatusLabel;
        private VisualElement _metricContainer;
        private ScrollView _operationsScroll;
        private VisualElement _operationsRail;
        private Label _activeToolLabel;
        private VisualElement _contextInspector;
        private Label _contextTitleLabel;
        private Label _contextSubtitleLabel;
        private Label _contextSummaryLabel;
        private Button _contextTabButton;
        private Button _tasksTabButton;
        private Button _entitiesTabButton;
        private Button _worldTabButton;
        private ScrollView _contextScroll;
        private VisualElement _contextCardContainer;
        private Label _currentToolHintLabel;
        private Label _hoverHintLabel;
        private Label _lastActionLabel;
        private Label _blockerHintLabel;
        private Label _warningHintLabel;
        private VisualElement _eventStripPanel;
        private VisualElement _worldOverlayLayer;
        private VisualElement _selectedWorldLabel;
        private Label _selectedWorldLabelTitle;
        private Label _selectedWorldLabelSubtitle;
        private VisualElement _hoverWorldLabel;
        private Label _hoverWorldLabelTitle;
        private Label _hoverWorldLabelSubtitle;
        private VisualElement _onboardingOverlay;
        private VisualElement _onboardingCard;
        private Label _onboardingProgressLabel;
        private Label _onboardingTitleLabel;
        private Label _onboardingBodyLabel;
        private Label _onboardingEmphasisLabel;
        private Label _onboardingTipLabel;
        private Button _onboardingBackButton;
        private Button _onboardingNextButton;
        private Button _onboardingSkipButton;
        private string _lastOperationsRailRenderKey = string.Empty;

        private bool _toolStrokeActive;
        private string _lastRenderKey = string.Empty;

        public static ClientWorldHudController Active { get; private set; }

        public VisualElement RootVisualElement => _root;

        public void Initialize(RuntimeGameConfig runtimeGameConfig, WorldSnapshot snapshot, LocalWorldDebugRenderer renderer, LocalColonyGameplayService gameplayService)
        {
            _runtimeGameConfig = runtimeGameConfig;
            _renderer = renderer;
            _gameplayService = gameplayService;
            _realTimeSimulationController ??= GetComponent<RealTimeSimulationController>();
            if (_realTimeSimulationController == null)
            {
                _realTimeSimulationController = gameObject.AddComponent<RealTimeSimulationController>();
            }

            _realTimeSimulationController.Initialize(runtimeGameConfig, gameplayService);
            _sceneNavigator ??= new SceneNavigator();
            _onboardingFlowService ??= new OnboardingFlowService();
            _actionService = new GameActionService(gameplayService, renderer, _realTimeSimulationController, _onboardingFlowService);
            _queryService = new GameQueryService(gameplayService, renderer, _realTimeSimulationController, _actionService, _onboardingFlowService);

            if (_gameplayService != null)
            {
                _gameplayService.WorldChanged -= HandleWorldChanged;
                _gameplayService.WorldChanged += HandleWorldChanged;
            }

            _actionService.StateChanged -= RefreshAll;
            _actionService.StateChanged += RefreshAll;
            _realTimeSimulationController.StateChanged -= RefreshAll;
            _realTimeSimulationController.StateChanged += RefreshAll;

            EnsureDocument();
            BuildUi();
            BindStaticUi();
            RefreshAll();
        }

        public HumanAgentHudState BuildHudState()
        {
            return _queryService != null ? _queryService.BuildHudState() : new HumanAgentHudState();
        }

        public GameSelectionState BuildSelectionState()
        {
            return _queryService != null ? _queryService.BuildSelectionState() : new GameSelectionState();
        }

        public ContextInspectorViewModel BuildContextInspectorState()
        {
            return _queryService != null ? _queryService.BuildContextInspectorState() : new ContextInspectorViewModel();
        }

        public List<EntitySummaryState> BuildEntitySummaries()
        {
            return _queryService != null ? _queryService.BuildEntitySummaries() : new List<EntitySummaryState>();
        }

        public List<BlockerSummaryState> BuildBlockerSummaries()
        {
            return _queryService != null ? _queryService.BuildBlockerSummaries() : new List<BlockerSummaryState>();
        }

        public List<GameActionDescriptor> BuildAvailableActions()
        {
            return _queryService != null ? _queryService.BuildAvailableActions() : new List<GameActionDescriptor>();
        }

        public GameActionParameterSchema GetActionSchema(GameActionId actionId)
        {
            return _actionService != null ? _actionService.GetSchema(actionId) : new GameActionParameterSchema { actionId = actionId };
        }

        public async UniTask<GameActionResult> ExecuteActionAsync(GameActionEnvelope envelope)
        {
            if (_actionService == null)
            {
                return new GameActionResult
                {
                    actionId = envelope != null ? envelope.actionId : GameActionId.None,
                    success = false,
                    blockedReason = "HUD is not initialized.",
                    message = "HUD is not initialized."
                };
            }

            GameActionResult result = await _actionService.ExecuteAsync(envelope);
            result.selectionAfter = BuildSelectionState();
            RefreshAll();
            return result;
        }

        public void RefreshImmediate()
        {
            RefreshAll();
        }

        private void Awake()
        {
            Active = this;
            _sceneNavigator ??= new SceneNavigator();
            EnsureDocument();
        }

        private void OnDestroy()
        {
            if (_gameplayService != null)
            {
                _gameplayService.WorldChanged -= HandleWorldChanged;
            }

            if (_actionService != null)
            {
                _actionService.StateChanged -= RefreshAll;
            }

            if (_realTimeSimulationController != null)
            {
                _realTimeSimulationController.StateChanged -= RefreshAll;
            }

            if (Active == this)
            {
                Active = null;
            }
        }

        private void Update()
        {
            if (_renderer == null || _gameplayService == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_actionService != null && _actionService.ActiveToolMode != WorldToolMode.None)
                {
                    ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.ClearTool), "EventStrip", "EscapeClearTool").Forget();
                    return;
                }

                ReturnToMenu().Forget();
                return;
            }

            if (Mouse.current == null)
            {
                return;
            }

            if (RuntimeInputCaptureService.Active != null && RuntimeInputCaptureService.Active.IsWorldInputBlocked())
            {
                ResetStroke();
                UpdateWorldLabels();
                RefreshIfStateChanged();
                return;
            }

            if (IsBrushToolActive() && Mouse.current.leftButton.isPressed)
            {
                if (!_toolStrokeActive)
                {
                    _toolStrokeActive = true;
                    _strokeCells.Clear();
                    _strokeEdges.Clear();
                }

                ApplyBrushTool().Forget();
                RefreshIfStateChanged();
                return;
            }

            ResetStroke();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_actionService.ActiveToolMode == WorldToolMode.None)
                {
                    SelectFromWorldClick().Forget();
                }
                else
                {
                    ApplyClickTool().Forget();
                }
            }

            if (_actionService.ActiveToolMode == WorldToolMode.None &&
                Mouse.current.rightButton.wasPressedThisFrame &&
                _renderer.HoverEdge.HasValue)
            {
                ExecuteActionAsync(_actionService.CreateEnvelope(
                    GameActionId.SelectEdge,
                    targetEdge: _renderer.HoverEdge.Value,
                    hasTargetEdge: true,
                    uiSource: CreateUiSource("WorldCanvas", "RightClickSelectEdge"))).Forget();
            }

            UpdateWorldLabels();
            RefreshIfStateChanged();
        }

        private async UniTaskVoid ReturnToMenu()
        {
            string menuScene = _runtimeGameConfig != null ? _runtimeGameConfig.MainMenuSceneName : SceneCatalog.MainMenu;
            await _sceneNavigator.LoadSingleAsync(menuScene);
        }

        private void EnsureDocument()
        {
            uiDocument ??= GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (visualTreeAsset == null)
            {
                visualTreeAsset = LoadDefaultHudVisualTree();
            }

            if (styleSheet == null)
            {
                styleSheet = LoadDefaultHudStyleSheet();
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

            uiDocument.panelSettings = panelSettings;
        }

        private static PanelSettings LoadDefaultPanelSettings()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPathConstants.RuntimePanelSettings);
#else
            return null;
#endif
        }

        private static VisualTreeAsset LoadDefaultHudVisualTree()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPathConstants.RuntimeHudUxml);
#else
            return null;
#endif
        }

        private static StyleSheet LoadDefaultHudStyleSheet()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(UiPathConstants.RuntimeHudUss);
#else
            return null;
#endif
        }

        private void BuildUi()
        {
            _root = uiDocument.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            {
                _root.styleSheets.Add(styleSheet);
            }

            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(_root);
            }
            else
            {
                BuildFallbackUi(_root);
            }

            _worldTopBar = _root.Q<VisualElement>("WorldTopBar");
            _worldNameLabel = _root.Q<Label>("WorldNameLabel");
            _tickLabel = _root.Q<Label>("TickLabel");
            _simulationLabel = _root.Q<Label>("SimulationLabel");
            _saveStateLabel = _root.Q<Label>("SaveStateLabel");
            _workerStatusLabel = _root.Q<Label>("WorkerStatusLabel");
            _growthStatusLabel = _root.Q<Label>("GrowthStatusLabel");
            _blockerStatusLabel = _root.Q<Label>("BlockerStatusLabel");
            _telemetryStatusLabel = _root.Q<Label>("TelemetryStatusLabel");
            _overlayStatusLabel = _root.Q<Label>("OverlayStatusLabel");
            _metricContainer = _root.Q<VisualElement>("WorldMetricsContainer");
            _operationsScroll = _root.Q<ScrollView>("OperationsRailScroll");
            _operationsRail = _root.Q<VisualElement>("OperationsRailContainer");
            _activeToolLabel = _root.Q<Label>("ActiveToolLabel");
            _contextInspector = _root.Q<VisualElement>("ContextInspectorPanel");
            _contextTitleLabel = _root.Q<Label>("ContextTitleLabel");
            _contextSubtitleLabel = _root.Q<Label>("ContextSubtitleLabel");
            _contextSummaryLabel = _root.Q<Label>("ContextSummaryLabel");
            _contextTabButton = _root.Q<Button>("ContextTabButton");
            _tasksTabButton = _root.Q<Button>("TasksTabButton");
            _entitiesTabButton = _root.Q<Button>("EntitiesTabButton");
            _worldTabButton = _root.Q<Button>("WorldTabButton");
            _contextScroll = _root.Q<ScrollView>("ContextInspectorScroll");
            _contextCardContainer = _root.Q<VisualElement>("ContextInspectorCards");
            _currentToolHintLabel = _root.Q<Label>("CurrentToolHintLabel");
            _hoverHintLabel = _root.Q<Label>("HoverHintLabel");
            _lastActionLabel = _root.Q<Label>("LastActionLabel");
            _blockerHintLabel = _root.Q<Label>("BlockerHintLabel");
            _warningHintLabel = _root.Q<Label>("WarningHintLabel");
            _eventStripPanel = _root.Q<VisualElement>("EventStripPanel");
            _worldOverlayLayer = _root.Q<VisualElement>("WorldOverlayLayer");
            _selectedWorldLabel = _root.Q<VisualElement>("SelectedWorldLabel");
            _selectedWorldLabelTitle = _root.Q<Label>("SelectedWorldLabelTitle");
            _selectedWorldLabelSubtitle = _root.Q<Label>("SelectedWorldLabelSubtitle");
            _hoverWorldLabel = _root.Q<VisualElement>("HoverWorldLabel");
            _hoverWorldLabelTitle = _root.Q<Label>("HoverWorldLabelTitle");
            _hoverWorldLabelSubtitle = _root.Q<Label>("HoverWorldLabelSubtitle");
            _onboardingOverlay = _root.Q<VisualElement>("OnboardingOverlay");
            _onboardingCard = _root.Q<VisualElement>("OnboardingCard");
            _onboardingProgressLabel = _root.Q<Label>("OnboardingProgressLabel");
            _onboardingTitleLabel = _root.Q<Label>("OnboardingTitleLabel");
            _onboardingBodyLabel = _root.Q<Label>("OnboardingBodyLabel");
            _onboardingEmphasisLabel = _root.Q<Label>("OnboardingEmphasisLabel");
            _onboardingTipLabel = _root.Q<Label>("OnboardingTipLabel");
            _onboardingBackButton = _root.Q<Button>("OnboardingBackButton");
            _onboardingNextButton = _root.Q<Button>("OnboardingNextButton");
            _onboardingSkipButton = _root.Q<Button>("OnboardingSkipButton");

            RuntimeInputCaptureService.Active?.ClearRegisteredElements();
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_root.Q<VisualElement>("OperationsRailPanel"));
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_contextInspector);
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_onboardingCard);

            if (_worldTopBar != null)
            {
                _worldTopBar.tooltip = _onboardingFlowService != null ? _onboardingFlowService.GetPanelTooltip("WorldTopBar") : string.Empty;
            }

            VisualElement operationsPanel = _root.Q<VisualElement>("OperationsRailPanel");
            if (operationsPanel != null)
            {
                operationsPanel.tooltip = _onboardingFlowService != null ? _onboardingFlowService.GetPanelTooltip("OperationsRailPanel") : string.Empty;
            }

            if (_contextInspector != null)
            {
                _contextInspector.tooltip = _onboardingFlowService != null ? _onboardingFlowService.GetPanelTooltip("ContextInspectorPanel") : string.Empty;
            }

            if (_eventStripPanel != null)
            {
                _eventStripPanel.tooltip = _onboardingFlowService != null ? _onboardingFlowService.GetPanelTooltip("EventStripPanel") : string.Empty;
            }
        }

        private void BuildFallbackUi(VisualElement root)
        {
            var shell = new VisualElement { name = "ClientWorldHudRoot" };
            shell.style.flexGrow = 1f;
            shell.style.flexDirection = FlexDirection.Column;

            var top = new VisualElement { name = "WorldTopBar" };
            top.style.flexDirection = FlexDirection.Column;
            top.Add(new Label { name = "WorldNameLabel", text = "World" });
            top.Add(new Label { name = "TickLabel", text = "Tick -" });
            top.Add(new Label { name = "SimulationLabel", text = "Paused" });
            top.Add(new Label { name = "SaveStateLabel", text = "Save" });
            top.Add(new Label { name = "WorkerStatusLabel", text = "Workers" });
            top.Add(new Label { name = "GrowthStatusLabel", text = "Growth" });
            top.Add(new Label { name = "BlockerStatusLabel", text = "Blockers" });
            top.Add(new Label { name = "TelemetryStatusLabel", text = "Telemetry" });
            top.Add(new Label { name = "OverlayStatusLabel", text = "Overlay" });
            top.Add(new VisualElement { name = "WorldMetricsContainer" });
            shell.Add(top);

            var body = new VisualElement { name = "HudBody" };
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Row;
            var operationsPanel = new VisualElement { name = "OperationsRailPanel" };
            operationsPanel.style.width = 360f;
            operationsPanel.Add(new Label { name = "ActiveToolLabel", text = "Active tool: None" });
            operationsPanel.Add(new ScrollView { name = "OperationsRailScroll" });
            operationsPanel.Q<ScrollView>("OperationsRailScroll").Add(new VisualElement { name = "OperationsRailContainer" });
            body.Add(operationsPanel);

            var spacer = new VisualElement { name = "HudCenterSpacer" };
            spacer.style.flexGrow = 1f;
            body.Add(spacer);

            var inspector = new VisualElement { name = "ContextInspectorPanel" };
            inspector.style.width = 420f;
            inspector.Add(new Label { name = "ContextTitleLabel", text = "Context" });
            inspector.Add(new Label { name = "ContextSubtitleLabel", text = "Nothing selected" });
            inspector.Add(new Label { name = "ContextSummaryLabel", text = "Select something in the world." });
            var tabs = new VisualElement { name = "InspectorTabBar" };
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.Add(new Button { name = "ContextTabButton", text = "Context" });
            tabs.Add(new Button { name = "TasksTabButton", text = "Tasks" });
            tabs.Add(new Button { name = "EntitiesTabButton", text = "Entities" });
            tabs.Add(new Button { name = "WorldTabButton", text = "World" });
            inspector.Add(tabs);
            inspector.Add(new ScrollView { name = "ContextInspectorScroll" });
            inspector.Q<ScrollView>("ContextInspectorScroll").Add(new VisualElement { name = "ContextInspectorCards" });
            body.Add(inspector);
            shell.Add(body);

            var worldOverlay = new VisualElement { name = "WorldOverlayLayer" };
            worldOverlay.style.position = Position.Absolute;
            worldOverlay.style.left = 0f;
            worldOverlay.style.right = 0f;
            worldOverlay.style.top = 0f;
            worldOverlay.style.bottom = 0f;

            var hoverLabel = new VisualElement { name = "HoverWorldLabel" };
            hoverLabel.Add(new Label { name = "HoverWorldLabelTitle" });
            hoverLabel.Add(new Label { name = "HoverWorldLabelSubtitle" });
            worldOverlay.Add(hoverLabel);

            var selectedLabel = new VisualElement { name = "SelectedWorldLabel" };
            selectedLabel.Add(new Label { name = "SelectedWorldLabelTitle" });
            selectedLabel.Add(new Label { name = "SelectedWorldLabelSubtitle" });
            worldOverlay.Add(selectedLabel);
            shell.Add(worldOverlay);

            var eventStrip = new VisualElement { name = "EventStripPanel" };
            eventStrip.Add(new Label { name = "CurrentToolHintLabel" });
            eventStrip.Add(new Label { name = "HoverHintLabel" });
            eventStrip.Add(new Label { name = "LastActionLabel" });
            eventStrip.Add(new Label { name = "BlockerHintLabel" });
            eventStrip.Add(new Label { name = "WarningHintLabel" });
            shell.Add(eventStrip);

            root.Add(shell);
        }

        private void BindStaticUi()
        {
            BindInspectorTabs();
            BindOnboardingControls();
            RefreshAll();
        }

        private void BindInspectorTabs()
        {
            BindTabButton(_contextTabButton, InspectorTabKind.Context, "ContextInspector", "TabContext");
            BindTabButton(_tasksTabButton, InspectorTabKind.Tasks, "ContextInspector", "TabTasks");
            BindTabButton(_entitiesTabButton, InspectorTabKind.Entities, "ContextInspector", "TabEntities");
            BindTabButton(_worldTabButton, InspectorTabKind.World, "ContextInspector", "TabWorld");
        }

        private void BindOnboardingControls()
        {
            if (_onboardingBackButton != null)
            {
                _onboardingBackButton.clicked += () =>
                {
                    ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.PreviousOnboardingStep, uiSource: CreateUiSource("Onboarding", "Back")), "Onboarding", "Back").Forget();
                };
            }

            if (_onboardingNextButton != null)
            {
                _onboardingNextButton.clicked += () =>
                {
                    ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.NextOnboardingStep, uiSource: CreateUiSource("Onboarding", "Next")), "Onboarding", "Next").Forget();
                };
            }

            if (_onboardingSkipButton != null)
            {
                _onboardingSkipButton.clicked += () =>
                {
                    ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SkipOnboarding, uiSource: CreateUiSource("Onboarding", "Skip")), "Onboarding", "Skip").Forget();
                };
            }
        }

        private void BindTabButton(Button button, InspectorTabKind tabKind, string panelId, string elementId)
        {
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                ExecuteUiAction(_actionService.CreateEnvelope(
                    GameActionId.SetInspectorTab,
                    inspectorTab: tabKind,
                    uiSource: CreateUiSource(panelId, elementId)), panelId, elementId).Forget();
            };
        }

        private void HandleWorldChanged(WorldSnapshot snapshot)
        {
            RefreshAll();
        }

        private void RefreshIfStateChanged()
        {
            string renderKey = BuildRenderKey();
            if (_lastRenderKey == renderKey)
            {
                return;
            }

            RefreshAll();
        }

        private string BuildRenderKey()
        {
            if (_renderer == null || _actionService == null || _gameplayService == null)
            {
                return string.Empty;
            }

            return string.Join("|",
                _gameplayService.Snapshot != null ? _gameplayService.Snapshot.simulationTick.ToString() : "-",
                _renderer.SelectedHex.HasValue ? _renderer.SelectedHex.Value.ToString() : "-",
                _renderer.SelectedEdge.HasValue ? _renderer.SelectedEdge.Value.ToString() : "-",
                _renderer.HoverHex.HasValue ? _renderer.HoverHex.Value.ToString() : "-",
                _renderer.HoverEdge.HasValue ? _renderer.HoverEdge.Value.ToString() : "-",
                _renderer.OverlayMode.ToString(),
                _actionService.ActiveToolMode.ToString(),
                _actionService.ActiveFloor.ToString(),
                _actionService.ActiveEdge.ToString(),
                _actionService.ActiveModule.ToString(),
                _actionService.ActiveLandmark.ToString(),
                _actionService.ActiveInspectorTab.ToString(),
                _actionService.SelectedEntityUid.ToString(),
                _actionService.LastActionMessage,
                _realTimeSimulationController != null ? _realTimeSimulationController.IsRunning.ToString() : "false",
                _realTimeSimulationController != null ? _realTimeSimulationController.SpeedMultiplier.ToString("0.##") : "1");
        }

        private void RefreshAll()
        {
            if (_queryService == null || _root == null)
            {
                return;
            }

            HumanAgentHudState hudState = _queryService.BuildRuntimeHudState();
            RenderWorldBar(hudState.worldBar);
            RenderOperationsRail(hudState.operationsRail);
            RenderContextInspector(hudState.contextInspector);
            RenderEventStrip(hudState.eventStrip);
            RenderOnboarding(hudState.onboarding);
            UpdateWorldLabels();
            _lastRenderKey = BuildRenderKey();
        }

        private void RenderWorldBar(WorldBarState worldBar)
        {
            _worldNameLabel.text = worldBar.worldName;
            _tickLabel.text = worldBar.tickText;
            _simulationLabel.text = worldBar.simulationText;
            _saveStateLabel.text = worldBar.saveStateText;
            _workerStatusLabel.text = worldBar.workerText;
            if (_growthStatusLabel != null)
            {
                _growthStatusLabel.text = worldBar.growthText;
            }
            _blockerStatusLabel.text = worldBar.blockerText;
            _telemetryStatusLabel.text = worldBar.telemetryText;
            _overlayStatusLabel.text = worldBar.overlayText;

            _metricContainer.Clear();
            for (int i = 0; i < worldBar.metrics.Count; i++)
            {
                _metricContainer.Add(CreateMetricChip(worldBar.metrics[i]));
            }
        }

        private void RenderOperationsRail(OperationsRailViewModel viewModel)
        {
            string renderKey = BuildOperationsRailRenderKey(viewModel);
            if (_lastOperationsRailRenderKey == renderKey)
            {
                return;
            }

            _activeToolLabel.text = viewModel.activeToolLabel;
            _operationsRail.Clear();
            for (int i = 0; i < viewModel.groups.Count; i++)
            {
                OperationsGroupViewModel group = viewModel.groups[i];
                var section = new VisualElement();
                section.AddToClassList("operations-group");
                var title = new Label(group.title);
                title.AddToClassList("panel-section-title");
                section.Add(title);

                var actionStack = new VisualElement();
                actionStack.AddToClassList("action-stack");
                for (int j = 0; j < group.actions.Count; j++)
                {
                    actionStack.Add(CreateActionButton(group.actions[j], "OperationsRail", $"{group.groupId}_{group.actions[j].actionId}_{j}"));
                }

                section.Add(actionStack);
                _operationsRail.Add(section);
            }

            _lastOperationsRailRenderKey = renderKey;
        }

        private void RenderContextInspector(ContextInspectorViewModel inspector)
        {
            _contextTitleLabel.text = inspector.title;
            _contextSubtitleLabel.text = inspector.subtitle;
            _contextSummaryLabel.text = inspector.summary;

            SetTabVisual(_contextTabButton, inspector.activeTab == InspectorTabKind.Context);
            SetTabVisual(_tasksTabButton, inspector.activeTab == InspectorTabKind.Tasks);
            SetTabVisual(_entitiesTabButton, inspector.activeTab == InspectorTabKind.Entities);
            SetTabVisual(_worldTabButton, inspector.activeTab == InspectorTabKind.World);

            _contextCardContainer.Clear();
            List<ContextCardViewModel> cards = inspector.activeTab switch
            {
                InspectorTabKind.Context => inspector.contextCards,
                InspectorTabKind.Tasks => inspector.taskCards,
                InspectorTabKind.Entities => inspector.entityCards,
                _ => inspector.worldCards
            };

            if (cards == null || cards.Count == 0)
            {
                _contextCardContainer.Add(CreateEmptyState("No data", "This view has nothing to show yet."));
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                _contextCardContainer.Add(CreateContextCard(cards[i]));
            }
        }

        private void RenderEventStrip(EventStripState eventStrip)
        {
            _currentToolHintLabel.text = eventStrip.currentToolText;
            _hoverHintLabel.text = eventStrip.hoverText;
            _lastActionLabel.text = eventStrip.lastActionText;
            _blockerHintLabel.text = eventStrip.blockerText;
            _warningHintLabel.text = eventStrip.warningText;
            _warningHintLabel.style.display = string.IsNullOrWhiteSpace(eventStrip.warningText) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RenderOnboarding(OnboardingStepViewModel onboarding)
        {
            if (_onboardingOverlay == null || _onboardingCard == null)
            {
                return;
            }

            bool isActive = onboarding != null && onboarding.isActive;
            _onboardingOverlay.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isActive)
            {
                ClearOnboardingHighlights();
                SetInspectorTabVisibility(_contextTabButton, true);
                SetInspectorTabVisibility(_tasksTabButton, true);
                SetInspectorTabVisibility(_entitiesTabButton, true);
                SetInspectorTabVisibility(_worldTabButton, true);
                return;
            }

            _onboardingProgressLabel.text = onboarding.progressText;
            _onboardingTitleLabel.text = onboarding.title;
            _onboardingBodyLabel.text = onboarding.body;
            _onboardingEmphasisLabel.text = onboarding.emphasis;
            _onboardingTipLabel.text = onboarding.tip;
            _onboardingBackButton.SetEnabled(onboarding.canGoBack);
            _onboardingSkipButton.SetEnabled(onboarding.canSkip);
            _onboardingNextButton.text = onboarding.isFinalStep ? "Finish Guide" : "Next Tip";

            ClearOnboardingHighlights();
            switch (onboarding.focusArea)
            {
                case OnboardingFocusArea.WorldTopBar:
                    _worldTopBar?.AddToClassList("hud-surface--highlighted");
                    _onboardingCard.AddToClassList("onboarding-card--top");
                    break;

                case OnboardingFocusArea.OperationsRail:
                    _root.Q<VisualElement>("OperationsRailPanel")?.AddToClassList("hud-surface--highlighted");
                    _onboardingCard.AddToClassList("onboarding-card--operations");
                    break;

                case OnboardingFocusArea.ContextInspector:
                    _contextInspector?.AddToClassList("hud-surface--highlighted");
                    _onboardingCard.AddToClassList("onboarding-card--inspector");
                    break;

                case OnboardingFocusArea.EventStrip:
                    _eventStripPanel?.AddToClassList("hud-surface--highlighted");
                    _onboardingCard.AddToClassList("onboarding-card--event");
                    break;

                default:
                    _onboardingCard.AddToClassList("onboarding-card--world");
                    break;
            }

            bool showContext = onboarding.visibleInspectorTabs == null || onboarding.visibleInspectorTabs.Count == 0 || onboarding.visibleInspectorTabs.Contains(InspectorTabKind.Context);
            bool showTasks = onboarding.visibleInspectorTabs == null || onboarding.visibleInspectorTabs.Count == 0 || onboarding.visibleInspectorTabs.Contains(InspectorTabKind.Tasks);
            bool showEntities = onboarding.visibleInspectorTabs == null || onboarding.visibleInspectorTabs.Count == 0 || onboarding.visibleInspectorTabs.Contains(InspectorTabKind.Entities);
            bool showWorld = onboarding.visibleInspectorTabs == null || onboarding.visibleInspectorTabs.Count == 0 || onboarding.visibleInspectorTabs.Contains(InspectorTabKind.World);
            SetInspectorTabVisibility(_contextTabButton, showContext);
            SetInspectorTabVisibility(_tasksTabButton, showTasks);
            SetInspectorTabVisibility(_entitiesTabButton, showEntities);
            SetInspectorTabVisibility(_worldTabButton, showWorld);
        }

        private void SetInspectorTabVisibility(Button button, bool isVisible)
        {
            if (button == null)
            {
                return;
            }

            button.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ClearOnboardingHighlights()
        {
            _worldTopBar?.RemoveFromClassList("hud-surface--highlighted");
            _root.Q<VisualElement>("OperationsRailPanel")?.RemoveFromClassList("hud-surface--highlighted");
            _contextInspector?.RemoveFromClassList("hud-surface--highlighted");
            _eventStripPanel?.RemoveFromClassList("hud-surface--highlighted");
            _onboardingCard?.RemoveFromClassList("onboarding-card--top");
            _onboardingCard?.RemoveFromClassList("onboarding-card--operations");
            _onboardingCard?.RemoveFromClassList("onboarding-card--inspector");
            _onboardingCard?.RemoveFromClassList("onboarding-card--event");
            _onboardingCard?.RemoveFromClassList("onboarding-card--world");
        }

        private void SetTabVisual(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList("inspector-tab--active", isActive);
        }

        private VisualElement CreateMetricChip(MetricState metric)
        {
            var chip = new VisualElement();
            chip.AddToClassList("metric-chip");
            chip.AddToClassList($"metric-chip--{metric.tone}");
            var label = new Label(metric.label);
            label.AddToClassList("metric-chip__label");
            var value = new Label(metric.value);
            value.AddToClassList("metric-chip__value");
            chip.Add(label);
            chip.Add(value);
            return chip;
        }

        private VisualElement CreateContextCard(ContextCardViewModel card)
        {
            var container = new VisualElement();
            container.AddToClassList("context-card");
            if (!string.IsNullOrWhiteSpace(card.tone))
            {
                container.AddToClassList($"context-card--{card.tone}");
            }

            var title = new Label(card.title);
            title.AddToClassList("context-card__title");
            container.Add(title);

            if (!string.IsNullOrWhiteSpace(card.subtitle))
            {
                var subtitle = new Label(card.subtitle);
                subtitle.AddToClassList("context-card__subtitle");
                container.Add(subtitle);
            }

            if (!string.IsNullOrWhiteSpace(card.summary))
            {
                var summary = new Label(card.summary);
                summary.AddToClassList("context-card__summary");
                container.Add(summary);
            }

            if (!string.IsNullOrWhiteSpace(card.emphasis))
            {
                var emphasis = new Label(card.emphasis);
                emphasis.AddToClassList("context-card__emphasis");
                container.Add(emphasis);
            }

            if (card.rows != null && card.rows.Count > 0)
            {
                var rowsContainer = new VisualElement();
                rowsContainer.AddToClassList("info-row-list");
                for (int i = 0; i < card.rows.Count; i++)
                {
                    rowsContainer.Add(CreateInfoRow(card.rows[i]));
                }

                container.Add(rowsContainer);
            }

            if (card.actions != null && card.actions.Count > 0)
            {
                var actionsContainer = new VisualElement();
                actionsContainer.AddToClassList("action-stack");
                for (int i = 0; i < card.actions.Count; i++)
                {
                    actionsContainer.Add(CreateActionButton(card.actions[i], "ContextInspector", $"{card.cardId}_{card.actions[i].actionId}_{i}"));
                }

                container.Add(actionsContainer);
            }

            return container;
        }

        private VisualElement CreateInfoRow(InfoRowState row)
        {
            var container = new VisualElement();
            container.AddToClassList("info-row");
            var label = new Label(row.label);
            label.AddToClassList("info-row__label");
            var value = new Label(row.value);
            value.AddToClassList("info-row__value");
            value.AddToClassList($"text-tone--{row.tone}");
            container.Add(label);
            container.Add(value);
            return container;
        }

        private VisualElement CreateEmptyState(string title, string detail)
        {
            var container = new VisualElement();
            container.AddToClassList("context-card");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("context-card__title");
            var detailLabel = new Label(detail);
            detailLabel.AddToClassList("context-card__summary");
            container.Add(titleLabel);
            container.Add(detailLabel);
            return container;
        }

        private Button CreateActionButton(GameActionDescriptor descriptor, string panelId, string elementId)
        {
            var button = new Button();
            button.tooltip = !string.IsNullOrWhiteSpace(descriptor.blockedReason)
                ? descriptor.blockedReason
                : !string.IsNullOrWhiteSpace(descriptor.description)
                    ? descriptor.description
                    : descriptor.hint;
            button.AddToClassList("action-button");
            if (descriptor.isActive)
            {
                button.AddToClassList("action-button--active");
            }

            if (!descriptor.isEnabled)
            {
                button.AddToClassList("action-button--disabled");
            }

            button.text = string.Empty;
            button.Clear();

            var content = new VisualElement();
            content.AddToClassList("action-button__content");

            var header = new VisualElement();
            header.AddToClassList("action-button__header");

            var title = new Label(descriptor.label);
            title.AddToClassList("action-button__title");
            header.Add(title);

            if (!string.IsNullOrWhiteSpace(descriptor.badgeText))
            {
                var badge = new Label(descriptor.badgeText);
                badge.AddToClassList("action-button__badge");
                header.Add(badge);
            }

            content.Add(header);

            string metaText = !string.IsNullOrWhiteSpace(descriptor.blockedReason)
                ? descriptor.blockedReason
                : !string.IsNullOrWhiteSpace(descriptor.description)
                    ? descriptor.description
                    : descriptor.hint;
            if (!string.IsNullOrWhiteSpace(metaText))
            {
                var meta = new Label(metaText);
                meta.AddToClassList("action-button__meta");
                if (!descriptor.isEnabled)
                {
                    meta.AddToClassList("action-button__meta--warning");
                }

                content.Add(meta);
            }

            button.Add(content);

            button.SetEnabled(descriptor.isEnabled);
            if (descriptor.isEnabled)
            {
                button.clicked += () =>
                {
                    ExecuteUiAction(CloneEnvelope(descriptor.envelope, panelId, elementId), panelId, elementId).Forget();
                };
            }

            return button;
        }

        private async UniTaskVoid ExecuteUiAction(GameActionEnvelope envelope, string panelId, string elementId)
        {
            GameActionResult result = await ExecuteActionAsync(envelope);
            if (!result.success)
            {
                Debug.LogWarning(result.blockedReason);
            }
        }

        private GameActionEnvelope CloneEnvelope(GameActionEnvelope sourceEnvelope, string panelId, string elementId)
        {
            return _actionService.CreateEnvelope(
                sourceEnvelope.actionId,
                targetCell: sourceEnvelope.targetCell,
                hasTargetCell: sourceEnvelope.hasTargetCell,
                targetEdge: sourceEnvelope.targetEdge,
                hasTargetEdge: sourceEnvelope.hasTargetEdge,
                targetEntityId: sourceEnvelope.targetEntityId,
                floorPurposeKind: sourceEnvelope.floorPurposeKind,
                edgeFeatureKind: sourceEnvelope.edgeFeatureKind,
                moduleKind: sourceEnvelope.moduleKind,
                landmarkKind: sourceEnvelope.landmarkKind,
                toolMode: sourceEnvelope.toolMode,
                overlayMode: sourceEnvelope.overlayMode,
                inspectorTab: sourceEnvelope.inspectorTab,
                tickCount: sourceEnvelope.tickCount,
                speedMultiplier: sourceEnvelope.speedMultiplier,
                desiredRunningState: sourceEnvelope.desiredRunningState,
                source: GameActionSource.HumanUi,
                uiSource: CreateUiSource(panelId, elementId));
        }

        private bool IsBrushToolActive()
        {
            if (_actionService == null)
            {
                return false;
            }

            return _actionService.ActiveToolMode == WorldToolMode.PaintFloor ||
                   _actionService.ActiveToolMode == WorldToolMode.BuildEdge ||
                   _actionService.ActiveToolMode == WorldToolMode.ClearCell ||
                   _actionService.ActiveToolMode == WorldToolMode.ClearEdge;
        }

        private async UniTaskVoid ApplyBrushTool()
        {
            switch (_actionService.ActiveToolMode)
            {
                case WorldToolMode.PaintFloor:
                    if (_renderer.HoverHex.HasValue && _strokeCells.Add(_renderer.HoverHex.Value))
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.PaintFloor,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            floorPurposeKind: _actionService.ActiveFloor,
                            uiSource: CreateUiSource("WorldCanvas", "BrushPaintFloor")));
                    }

                    break;

                case WorldToolMode.BuildEdge:
                    if (_renderer.HoverEdge.HasValue && _strokeEdges.Add(_renderer.HoverEdge.Value))
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.BuildEdge,
                            targetEdge: _renderer.HoverEdge.Value,
                            hasTargetEdge: true,
                            edgeFeatureKind: _actionService.ActiveEdge,
                            uiSource: CreateUiSource("WorldCanvas", "BrushBuildEdge")));
                    }

                    break;

                case WorldToolMode.ClearCell:
                    if (_renderer.HoverHex.HasValue && _strokeCells.Add(_renderer.HoverHex.Value))
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.ClearCell,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            uiSource: CreateUiSource("WorldCanvas", "BrushClearCell")));
                    }

                    break;

                case WorldToolMode.ClearEdge:
                    if (_renderer.HoverEdge.HasValue && _strokeEdges.Add(_renderer.HoverEdge.Value))
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.ClearEdge,
                            targetEdge: _renderer.HoverEdge.Value,
                            hasTargetEdge: true,
                            uiSource: CreateUiSource("WorldCanvas", "BrushClearEdge")));
                    }

                    break;
            }
        }

        private async UniTaskVoid ApplyClickTool()
        {
            switch (_actionService.ActiveToolMode)
            {
                case WorldToolMode.PaintFloor:
                    if (_renderer.HoverHex.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.PaintFloor,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            floorPurposeKind: _actionService.ActiveFloor,
                            uiSource: CreateUiSource("WorldCanvas", "ClickPaintFloor")));
                    }

                    break;

                case WorldToolMode.BuildEdge:
                    if (_renderer.HoverEdge.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.BuildEdge,
                            targetEdge: _renderer.HoverEdge.Value,
                            hasTargetEdge: true,
                            edgeFeatureKind: _actionService.ActiveEdge,
                            uiSource: CreateUiSource("WorldCanvas", "ClickBuildEdge")));
                    }

                    break;

                case WorldToolMode.PlaceModule:
                    if (_renderer.HoverHex.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.PlaceModule,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            moduleKind: _actionService.ActiveModule,
                            uiSource: CreateUiSource("WorldCanvas", "ClickPlaceModule")));
                    }

                    break;

                case WorldToolMode.PlaceLandmark:
                    if (_renderer.HoverHex.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.PlaceLandmark,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            landmarkKind: _actionService.ActiveLandmark,
                            uiSource: CreateUiSource("WorldCanvas", "ClickPlaceLandmark")));
                    }

                    break;

                case WorldToolMode.ClearCell:
                    if (_renderer.HoverHex.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.ClearCell,
                            targetCell: _renderer.HoverHex.Value,
                            hasTargetCell: true,
                            uiSource: CreateUiSource("WorldCanvas", "ClickClearCell")));
                    }

                    break;

                case WorldToolMode.ClearEdge:
                    if (_renderer.HoverEdge.HasValue)
                    {
                        await ExecuteActionAsync(_actionService.CreateEnvelope(
                            GameActionId.ClearEdge,
                            targetEdge: _renderer.HoverEdge.Value,
                            hasTargetEdge: true,
                            uiSource: CreateUiSource("WorldCanvas", "ClickClearEdge")));
                    }

                    break;
            }
        }

        private async UniTaskVoid SelectFromWorldClick()
        {
            if (_renderer == null || _actionService == null || _gameplayService == null)
            {
                return;
            }

            if (_renderer.HoverHex.HasValue)
            {
                List<UnitSnapshot> units = _gameplayService.GetUnitsAtCell(_renderer.HoverHex.Value);
                if (units.Count > 0)
                {
                    await ExecuteActionAsync(_actionService.CreateEnvelope(
                        GameActionId.SelectEntity,
                        targetEntityId: units[0].entityUid.ToString(),
                        uiSource: CreateUiSource("WorldCanvas", "ClickSelectEntity")));
                    return;
                }

                await ExecuteActionAsync(_actionService.CreateEnvelope(
                    GameActionId.SelectCell,
                    targetCell: _renderer.HoverHex.Value,
                    hasTargetCell: true,
                    uiSource: CreateUiSource("WorldCanvas", "ClickSelectCell")));
                return;
            }

            if (_renderer.HoverEdge.HasValue)
            {
                await ExecuteActionAsync(_actionService.CreateEnvelope(
                    GameActionId.SelectEdge,
                    targetEdge: _renderer.HoverEdge.Value,
                    hasTargetEdge: true,
                    uiSource: CreateUiSource("WorldCanvas", "ClickSelectEdge")));
            }
        }

        private void UpdateWorldLabels()
        {
            if (_root == null || _renderer == null || UnityEngine.Camera.main == null)
            {
                return;
            }

            GameSelectionState selection = BuildSelectionState();
            UpdateWorldLabel(_selectedWorldLabel, _selectedWorldLabelTitle, _selectedWorldLabelSubtitle, BuildSelectedWorldLabel(selection), true);
            UpdateWorldLabel(_hoverWorldLabel, _hoverWorldLabelTitle, _hoverWorldLabelSubtitle, BuildHoverWorldLabel(selection), false);
        }

        private void UpdateWorldLabel(VisualElement container, Label titleLabel, Label subtitleLabel, WorldLabelState state, bool preferAbove)
        {
            if (container == null || titleLabel == null || subtitleLabel == null)
            {
                return;
            }

            if (state == null || !state.isVisible || UnityEngine.Camera.main == null)
            {
                container.style.display = DisplayStyle.None;
                return;
            }

            Vector3 screenPosition = UnityEngine.Camera.main.WorldToScreenPoint(state.worldPosition);
            if (screenPosition.z <= 0f)
            {
                container.style.display = DisplayStyle.None;
                return;
            }

            titleLabel.text = state.title;
            subtitleLabel.text = state.subtitle;
            container.style.display = DisplayStyle.Flex;
            container.style.left = Mathf.Clamp(screenPosition.x + 12f, 18f, Screen.width - 290f);
            float top = Screen.height - screenPosition.y + (preferAbove ? -56f : 16f);
            container.style.top = Mathf.Clamp(top, 18f, Screen.height - 96f);
        }

        private WorldLabelState BuildSelectedWorldLabel(GameSelectionState selection)
        {
            if (_actionService != null &&
                _actionService.SelectedEntityUid != EntityUid.None &&
                _renderer.TryGetUnitWorldPosition(_actionService.SelectedEntityUid, out Vector3 unitPosition))
            {
                List<UnitSnapshot> units = _renderer.SelectedHex.HasValue
                    ? _gameplayService.GetUnitsAtCell(_renderer.SelectedHex.Value)
                    : new List<UnitSnapshot>();
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i].entityUid == _actionService.SelectedEntityUid)
                    {
                        return new WorldLabelState
                        {
                            isVisible = true,
                            worldPosition = unitPosition,
                            title = units[i].unitRole.ToString(),
                            subtitle = units[i].currentJobType == JobType.Idle ? "Selected unit" : units[i].currentJobType.ToString()
                        };
                    }
                }
            }

            if (selection.hasSelectedCell && _renderer.TryGetCellWorldPosition(selection.selectedCell, out Vector3 cellPosition))
            {
                return BuildCellWorldLabel(selection.selectedCell, cellPosition, true);
            }

            if (selection.hasSelectedEdge && _renderer.TryGetEdgeWorldPosition(selection.selectedEdge, out Vector3 edgePosition))
            {
                HexEdgeState edgeState = _gameplayService.GetEdgeState(selection.selectedEdge);
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = edgePosition,
                    title = edgeState.isBuilt ? edgeState.featureKind.ToString() : "Edge",
                    subtitle = selection.selectedEdge.ToString()
                };
            }

            return null;
        }

        private WorldLabelState BuildHoverWorldLabel(GameSelectionState selection)
        {
            if (_actionService != null && _actionService.ActiveToolMode != WorldToolMode.None)
            {
                if (selection.hasHoveredCell && _renderer.TryGetCellWorldPosition(selection.hoveredCell, out Vector3 actionCell))
                {
                    return BuildActionPreviewLabel(actionCell, selection);
                }

                if (selection.hasHoveredEdge && _renderer.TryGetEdgeWorldPosition(selection.hoveredEdge, out Vector3 actionEdge))
                {
                    return BuildActionPreviewLabel(actionEdge, selection);
                }
            }

            if (selection.hasHoveredCell &&
                (!selection.hasSelectedCell || selection.selectedCell != selection.hoveredCell) &&
                _renderer.TryGetCellWorldPosition(selection.hoveredCell, out Vector3 cellPosition))
            {
                return BuildCellWorldLabel(selection.hoveredCell, cellPosition, false);
            }

            if (selection.hasHoveredEdge &&
                (!selection.hasSelectedEdge || selection.selectedEdge != selection.hoveredEdge) &&
                _renderer.TryGetEdgeWorldPosition(selection.hoveredEdge, out Vector3 edgePosition))
            {
                HexEdgeState edgeState = _gameplayService.GetEdgeState(selection.hoveredEdge);
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = edgePosition,
                    title = edgeState.isBuilt ? edgeState.featureKind.ToString() : "Edge",
                    subtitle = "Right-click to select"
                };
            }

            return null;
        }

        private WorldLabelState BuildActionPreviewLabel(Vector3 worldPosition, GameSelectionState selection)
        {
            GameActionDescriptor descriptor = null;
            switch (_actionService.ActiveToolMode)
            {
                case WorldToolMode.PaintFloor when selection.hasHoveredCell:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.PaintFloor, targetCell: selection.hoveredCell, hasTargetCell: true, floorPurposeKind: _actionService.ActiveFloor));
                    break;
                case WorldToolMode.BuildEdge when selection.hasHoveredEdge:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.BuildEdge, targetEdge: selection.hoveredEdge, hasTargetEdge: true, edgeFeatureKind: _actionService.ActiveEdge));
                    break;
                case WorldToolMode.PlaceModule when selection.hasHoveredCell:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.PlaceModule, targetCell: selection.hoveredCell, hasTargetCell: true, moduleKind: _actionService.ActiveModule));
                    break;
                case WorldToolMode.PlaceLandmark when selection.hasHoveredCell:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.PlaceLandmark, targetCell: selection.hoveredCell, hasTargetCell: true, landmarkKind: _actionService.ActiveLandmark));
                    break;
                case WorldToolMode.ClearCell when selection.hasHoveredCell:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.ClearCell, targetCell: selection.hoveredCell, hasTargetCell: true));
                    break;
                case WorldToolMode.ClearEdge when selection.hasHoveredEdge:
                    descriptor = _actionService.Describe(_actionService.CreateEnvelope(GameActionId.ClearEdge, targetEdge: selection.hoveredEdge, hasTargetEdge: true));
                    break;
            }

            if (descriptor == null)
            {
                return null;
            }

            return new WorldLabelState
            {
                isVisible = true,
                worldPosition = worldPosition,
                title = descriptor.label,
                subtitle = descriptor.isEnabled ? "Click to apply" : descriptor.blockedReason
            };
        }

        private WorldLabelState BuildCellWorldLabel(HexCoord cell, Vector3 worldPosition, bool selected)
        {
            List<UnitSnapshot> units = _gameplayService.GetUnitsAtCell(cell);
            if (units.Count > 0)
            {
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = worldPosition + Vector3.up * 0.25f,
                    title = units[0].unitRole.ToString(),
                    subtitle = selected ? "Selected unit tile" : "Click to select unit"
                };
            }

            HexCellState state = _gameplayService.GetCellState(cell);
            if (state.module != null && state.module.isBuilt)
            {
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = worldPosition,
                    title = state.module.moduleKind.ToString(),
                    subtitle = selected ? "Selected module" : "Click to inspect"
                };
            }

            List<LandmarkState> landmarks = _gameplayService.GetLandmarksAtCell(cell);
            if (landmarks.Count > 0)
            {
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = worldPosition,
                    title = landmarks[0].landmarkKind.ToString(),
                    subtitle = selected ? "Selected landmark" : "Click to inspect"
                };
            }

            List<ItemStackEntity> stacks = _gameplayService.GetItemStacksAtCell(cell);
            if (stacks.Count > 0)
            {
                return new WorldLabelState
                {
                    isVisible = true,
                    worldPosition = worldPosition,
                    title = stacks[0].resourceType.ToString(),
                    subtitle = $"{stacks[0].amount} on this tile"
                };
            }

            string title = state.floor != null && state.floor.isBuilt ? $"{state.floor.purposeKind} Tile" : "Tile";
            return new WorldLabelState
            {
                isVisible = true,
                worldPosition = worldPosition,
                title = title,
                subtitle = selected ? cell.ToString() : "Click to inspect"
            };
        }

        private void ResetStroke()
        {
            _toolStrokeActive = false;
            _strokeCells.Clear();
            _strokeEdges.Clear();
        }

        private sealed class WorldLabelState
        {
            public bool isVisible;
            public Vector3 worldPosition;
            public string title;
            public string subtitle;
        }

        private static string BuildOperationsRailRenderKey(OperationsRailViewModel viewModel)
        {
            var parts = new List<string> { viewModel.activeToolLabel, viewModel.groups.Count.ToString() };
            for (int i = 0; i < viewModel.groups.Count; i++)
            {
                OperationsGroupViewModel group = viewModel.groups[i];
                parts.Add(group.groupId);
                parts.Add(group.actions.Count.ToString());
                for (int j = 0; j < group.actions.Count; j++)
                {
                    GameActionDescriptor action = group.actions[j];
                    parts.Add($"{action.actionId}:{action.isEnabled}:{action.isActive}:{action.badgeText}:{action.blockedReason}:{action.label}");
                }
            }

            return string.Join("|", parts);
        }

        private static SessionUiSource CreateUiSource(string panelId, string elementId)
        {
            return new SessionUiSource
            {
                panelId = panelId,
                elementId = elementId,
                controller = nameof(ClientWorldHudController)
            };
        }
    }
}
