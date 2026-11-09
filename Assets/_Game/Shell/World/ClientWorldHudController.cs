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
using Ggj2026.Game.Shell.Audio;
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
        private Label _nextStepLabel;
        private VisualElement _contextInspector;
        private Label _contextTitleLabel;
        private Label _contextSubtitleLabel;
        private Label _contextSummaryLabel;
        private Label _contextActionHintLabel;
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
        private VisualElement _controlHintsBar;
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
        private bool _hasHudStylesheet;
        private bool _fallbackStylesApplied;

        private bool _toolStrokeActive;
        private string _lastRenderKey = string.Empty;
        private string _pendingMilestoneMessage;
        private float _milestoneDisplayEndTime;

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
                _gameplayService.MilestoneAchieved -= HandleMilestoneAchieved;
                _gameplayService.MilestoneAchieved += HandleMilestoneAchieved;
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
                _gameplayService.MilestoneAchieved -= HandleMilestoneAchieved;
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

                if (PauseMenuController.Active != null)
                {
                    PauseMenuController.Active.TogglePause();
                }
                else
                {
                    ReturnToMenu().Forget();
                }

                return;
            }

            if (PauseMenuController.Active != null && PauseMenuController.Active.IsPaused)
            {
                return;
            }

            HandleKeyboardShortcuts();

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

        private void HandleKeyboardShortcuts()
        {
            if (_actionService == null || Keyboard.current == null) return;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(
                    GameActionId.SetRealtimeRunning,
                    desiredRunningState: _realTimeSimulationController != null && !_realTimeSimulationController.IsRunning,
                    uiSource: CreateUiSource("Keyboard", "Space")), "Keyboard", "ToggleSim").Forget();
            }

            if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
            {
                float newSpeed = _realTimeSimulationController != null
                    ? Mathf.Min(_realTimeSimulationController.SpeedMultiplier * 2f, 8f)
                    : 2f;
                ExecuteUiAction(_actionService.CreateEnvelope(
                    GameActionId.AdjustSimulationSpeed,
                    speedMultiplier: newSpeed,
                    uiSource: CreateUiSource("Keyboard", "SpeedUp")), "Keyboard", "SpeedUp").Forget();
            }

            if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
            {
                float newSpeed = _realTimeSimulationController != null
                    ? Mathf.Max(_realTimeSimulationController.SpeedMultiplier * 0.5f, 0.25f)
                    : 0.5f;
                ExecuteUiAction(_actionService.CreateEnvelope(
                    GameActionId.AdjustSimulationSpeed,
                    speedMultiplier: newSpeed,
                    uiSource: CreateUiSource("Keyboard", "SlowDown")), "Keyboard", "SlowDown").Forget();
            }

            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                InspectorTabKind next = _actionService.ActiveInspectorTab switch
                {
                    InspectorTabKind.Context => InspectorTabKind.Tasks,
                    InspectorTabKind.Tasks => InspectorTabKind.Entities,
                    InspectorTabKind.Entities => InspectorTabKind.World,
                    _ => InspectorTabKind.Context
                };
                ExecuteUiAction(_actionService.CreateEnvelope(
                    GameActionId.SetInspectorTab,
                    inspectorTab: next,
                    uiSource: CreateUiSource("Keyboard", "Tab")), "Keyboard", "CycleTab").Forget();
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetFloorTool, uiSource: CreateUiSource("Keyboard", "1")), "Keyboard", "FloorTool").Forget();
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetEdgeTool, uiSource: CreateUiSource("Keyboard", "2")), "Keyboard", "EdgeTool").Forget();
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetModuleTool, uiSource: CreateUiSource("Keyboard", "3")), "Keyboard", "ModuleTool").Forget();
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetLandmarkTool, uiSource: CreateUiSource("Keyboard", "4")), "Keyboard", "LandmarkTool").Forget();
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetClearCellTool, uiSource: CreateUiSource("Keyboard", "5")), "Keyboard", "ClearCellTool").Forget();
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                ExecuteUiAction(_actionService.CreateEnvelope(GameActionId.SetClearEdgeTool, uiSource: CreateUiSource("Keyboard", "6")), "Keyboard", "ClearEdgeTool").Forget();
            }
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
            _fallbackStylesApplied = false;
            AttachHudStyleSheets();

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
            _nextStepLabel = _root.Q<Label>("NextStepLabel");
            _contextInspector = _root.Q<VisualElement>("ContextInspectorPanel");
            _contextTitleLabel = _root.Q<Label>("ContextTitleLabel");
            _contextSubtitleLabel = _root.Q<Label>("ContextSubtitleLabel");
            _contextSummaryLabel = _root.Q<Label>("ContextSummaryLabel");
            _contextActionHintLabel = _root.Q<Label>("ContextActionHintLabel");
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
            _controlHintsBar = _root.Q<VisualElement>("ControlHintsBar");
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

            var _shellEl = _root.Q<VisualElement>("ClientWorldHudRoot");
            if (_shellEl != null) _shellEl.pickingMode = PickingMode.Ignore;
            var _bodyEl = _root.Q<VisualElement>("HudBody");
            if (_bodyEl != null) _bodyEl.pickingMode = PickingMode.Ignore;
            var _spacerEl = _root.Q<VisualElement>("HudCenterSpacer");
            if (_spacerEl != null) _spacerEl.pickingMode = PickingMode.Ignore;
            if (_worldOverlayLayer != null) _worldOverlayLayer.pickingMode = PickingMode.Ignore;
            if (_selectedWorldLabel != null) _selectedWorldLabel.pickingMode = PickingMode.Ignore;
            if (_selectedWorldLabelTitle != null) _selectedWorldLabelTitle.pickingMode = PickingMode.Ignore;
            if (_selectedWorldLabelSubtitle != null) _selectedWorldLabelSubtitle.pickingMode = PickingMode.Ignore;
            if (_hoverWorldLabel != null) _hoverWorldLabel.pickingMode = PickingMode.Ignore;
            if (_hoverWorldLabelTitle != null) _hoverWorldLabelTitle.pickingMode = PickingMode.Ignore;
            if (_hoverWorldLabelSubtitle != null) _hoverWorldLabelSubtitle.pickingMode = PickingMode.Ignore;
            if (_onboardingOverlay != null) _onboardingOverlay.pickingMode = PickingMode.Ignore;

            RuntimeInputCaptureService.Active?.ClearRegisteredElements();
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_worldTopBar);
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_root.Q<VisualElement>("OperationsRailPanel"));
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_contextInspector);
            RuntimeInputCaptureService.Active?.RegisterBlockingElement(_eventStripPanel);
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

            if (!_hasHudStylesheet)
            {
                ApplyFallbackRuntimeStyles();
            }
        }

        private void AttachHudStyleSheets()
        {
            _hasHudStylesheet = false;

            StyleSheet defaultHudStyle = LoadDefaultHudStyleSheet();
            if (defaultHudStyle != null && !_root.styleSheets.Contains(defaultHudStyle))
            {
                _root.styleSheets.Add(defaultHudStyle);
                _hasHudStylesheet = true;
            }

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            {
                _root.styleSheets.Add(styleSheet);
                _hasHudStylesheet = true;
            }

            if (!_hasHudStylesheet && styleSheet != null)
            {
                _hasHudStylesheet = true;
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
            operationsPanel.Add(new Label { text = "Actions" });
            operationsPanel.Add(new Label { text = "Choose an action, then click or drag on the world." });
            operationsPanel.Add(new Label { name = "ActiveToolLabel", text = "Active tool: None" });
            operationsPanel.Add(new Label { name = "NextStepLabel", text = "Next step: Select a tile to begin." });
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
            inspector.Add(new Label { name = "ContextActionHintLabel", text = "Suggested action: Select a tile, edge, or unit." });
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
            worldOverlay.pickingMode = PickingMode.Ignore;
            worldOverlay.style.position = Position.Absolute;
            worldOverlay.style.left = 0f;
            worldOverlay.style.right = 0f;
            worldOverlay.style.top = 0f;
            worldOverlay.style.bottom = 0f;

            var hoverLabel = new VisualElement { name = "HoverWorldLabel" };
            hoverLabel.pickingMode = PickingMode.Ignore;
            hoverLabel.Add(new Label { name = "HoverWorldLabelTitle" });
            hoverLabel.Add(new Label { name = "HoverWorldLabelSubtitle" });
            worldOverlay.Add(hoverLabel);

            var selectedLabel = new VisualElement { name = "SelectedWorldLabel" };
            selectedLabel.pickingMode = PickingMode.Ignore;
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

        private void HandleMilestoneAchieved(ColonyMilestone milestone)
        {
            if (_gameplayService == null)
            {
                return;
            }

            _pendingMilestoneMessage = _gameplayService.LastMilestoneMessage;
            _milestoneDisplayEndTime = Time.time + 5f;
            ColonyAudioService.Active?.PlaySound(ColonySoundId.MilestoneFanfare);
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
            RenderGuidance(hudState);
            RenderEventStrip(hudState.eventStrip);
            RenderMilestoneNotification();
            RenderOnboarding(hudState.onboarding);
            RenderControlHints();
            UpdateWorldLabels();
            EnsureReadableStyles();
            _lastRenderKey = BuildRenderKey();
        }

        private void RenderMilestoneNotification()
        {
            if (string.IsNullOrEmpty(_pendingMilestoneMessage) || Time.time > _milestoneDisplayEndTime)
            {
                _pendingMilestoneMessage = null;
                return;
            }

            if (_warningHintLabel != null)
            {
                _warningHintLabel.text = _pendingMilestoneMessage;
                _warningHintLabel.style.display = DisplayStyle.Flex;
            }
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

        private void RenderGuidance(HumanAgentHudState hudState)
        {
            if (_nextStepLabel != null)
            {
                string blockerText = hudState?.worldBar?.blockerText ?? string.Empty;
                if (IsActionToolMode(_actionService != null ? _actionService.ActiveToolMode : WorldToolMode.None))
                {
                    _nextStepLabel.text = "Next: Click or drag on the world to apply the current tool.";
                }
                else if (!string.IsNullOrWhiteSpace(blockerText) &&
                         !blockerText.Equals("No blockers", System.StringComparison.OrdinalIgnoreCase))
                {
                    _nextStepLabel.text = $"Next: Resolve blocker - {blockerText}";
                }
                else if (TryFindFirstEnabledContextAction(hudState?.contextInspector, out GameActionDescriptor contextAction))
                {
                    _nextStepLabel.text = $"Next: {contextAction.label}";
                }
                else if (TryFindFirstEnabledAction(hudState?.operationsRail, out GameActionDescriptor opAction))
                {
                    _nextStepLabel.text = $"Next: {opAction.label}";
                }
                else
                {
                    _nextStepLabel.text = "Next: Select a tile in the world.";
                }
            }

            if (_contextActionHintLabel != null)
            {
                if (TryFindFirstEnabledContextAction(hudState?.contextInspector, out GameActionDescriptor contextAction))
                {
                    _contextActionHintLabel.text = $"Suggested action: {contextAction.label}";
                }
                else if (TryFindFirstBlockedContextAction(hudState?.contextInspector, out GameActionDescriptor blockedAction) &&
                         !string.IsNullOrWhiteSpace(blockedAction.blockedReason))
                {
                    _contextActionHintLabel.text = $"Blocked: {blockedAction.blockedReason}";
                }
                else
                {
                    _contextActionHintLabel.text = "Suggested action: Select a tile, edge, or unit.";
                }
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
                _renderer?.SetOnboardingMarker(null);
                SetInspectorTabVisibility(_contextTabButton, true);
                SetInspectorTabVisibility(_tasksTabButton, true);
                SetInspectorTabVisibility(_entitiesTabButton, true);
                SetInspectorTabVisibility(_worldTabButton, true);
                return;
            }

            _renderer?.SetOnboardingMarker(onboarding.hasWorldMarkerCell ? onboarding.worldMarkerCell : (HexCoord?)null);

            _onboardingProgressLabel.text = onboarding.progressText;
            _onboardingTitleLabel.text = onboarding.title;
            _onboardingBodyLabel.text = onboarding.body;
            _onboardingEmphasisLabel.text = onboarding.emphasis;
            string tipText = onboarding.tip;
            if (onboarding.hasTryItAction)
            {
                tipText = string.IsNullOrWhiteSpace(tipText)
                    ? "Try it now!"
                    : tipText + " \u2014 Try it now!";
            }
            _onboardingTipLabel.text = tipText;
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

        private void RenderControlHints()
        {
            if (_controlHintsBar == null) return;
            _controlHintsBar.Clear();
            InputDeviceKind device = InputDeviceService.Active != null ? InputDeviceService.Active.CurrentDevice : InputDeviceKind.KeyboardMouse;

            _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("ToggleSim", device), "Play/Pause", device));
            _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("SpeedUp", device), "Faster", device));
            _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("SlowDown", device), "Slower", device));
            _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("CycleTab", device), "Cycle Tab", device));
            _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("Pause", device), "Pause", device));

            if (_actionService != null && _actionService.ActiveToolMode != WorldToolMode.None)
            {
                _controlHintsBar.Add(KeycapStyleFactory.CreateHint(KeycapStyleFactory.GetKeyText("CancelTool", device), "Cancel Tool", device));
            }
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

            string metaText = string.Empty;
            if (!string.IsNullOrWhiteSpace(descriptor.blockedReason))
            {
                metaText = descriptor.blockedReason;
            }
            else if (descriptor.isActive && !string.IsNullOrWhiteSpace(descriptor.hint))
            {
                metaText = descriptor.hint;
            }

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

        private static bool TryFindFirstEnabledAction(OperationsRailViewModel rail, out GameActionDescriptor descriptor)
        {
            descriptor = null;
            if (rail?.groups == null)
            {
                return false;
            }

            for (int i = 0; i < rail.groups.Count; i++)
            {
                OperationsGroupViewModel group = rail.groups[i];
                if (group?.actions == null)
                {
                    continue;
                }

                for (int j = 0; j < group.actions.Count; j++)
                {
                    GameActionDescriptor candidate = group.actions[j];
                    if (candidate != null && candidate.isEnabled)
                    {
                        descriptor = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryFindFirstEnabledContextAction(ContextInspectorViewModel inspector, out GameActionDescriptor descriptor)
        {
            descriptor = null;
            if (inspector == null)
            {
                return false;
            }

            List<ContextCardViewModel> cards = inspector.activeTab switch
            {
                InspectorTabKind.Context => inspector.contextCards,
                InspectorTabKind.Tasks => inspector.taskCards,
                InspectorTabKind.Entities => inspector.entityCards,
                _ => inspector.worldCards
            };

            if (cards == null)
            {
                return false;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                ContextCardViewModel card = cards[i];
                if (card?.actions == null)
                {
                    continue;
                }

                for (int j = 0; j < card.actions.Count; j++)
                {
                    GameActionDescriptor candidate = card.actions[j];
                    if (candidate != null && candidate.isEnabled)
                    {
                        descriptor = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryFindFirstBlockedContextAction(ContextInspectorViewModel inspector, out GameActionDescriptor descriptor)
        {
            descriptor = null;
            if (inspector == null)
            {
                return false;
            }

            List<ContextCardViewModel> cards = inspector.activeTab switch
            {
                InspectorTabKind.Context => inspector.contextCards,
                InspectorTabKind.Tasks => inspector.taskCards,
                InspectorTabKind.Entities => inspector.entityCards,
                _ => inspector.worldCards
            };

            if (cards == null)
            {
                return false;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                ContextCardViewModel card = cards[i];
                if (card?.actions == null)
                {
                    continue;
                }

                for (int j = 0; j < card.actions.Count; j++)
                {
                    GameActionDescriptor candidate = card.actions[j];
                    if (candidate == null || candidate.isEnabled)
                    {
                        continue;
                    }

                    descriptor = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsActionToolMode(WorldToolMode mode)
        {
            return mode == WorldToolMode.PaintFloor ||
                   mode == WorldToolMode.BuildEdge ||
                   mode == WorldToolMode.PlaceModule ||
                   mode == WorldToolMode.PlaceLandmark ||
                   mode == WorldToolMode.ClearCell ||
                   mode == WorldToolMode.ClearEdge;
        }

        private void ApplyFallbackRuntimeStyles()
        {
            Color shellText = new Color(0.94f, 0.96f, 0.97f, 1f);
            Color panel = new Color(0.08f, 0.11f, 0.14f, 0.95f);
            Color panelStrong = new Color(0.12f, 0.16f, 0.2f, 0.97f);
            _fallbackStylesApplied = true;

            VisualElement shell = _root.Q<VisualElement>("ClientWorldHudRoot");
            if (shell != null)
            {
                shell.style.flexDirection = FlexDirection.Column;
                shell.style.justifyContent = Justify.SpaceBetween;
                shell.style.paddingLeft = 16f;
                shell.style.paddingRight = 16f;
                shell.style.paddingTop = 14f;
                shell.style.paddingBottom = 14f;
            }

            if (_worldTopBar != null)
            {
                _worldTopBar.style.backgroundColor = panelStrong;
                _worldTopBar.style.paddingLeft = 16f;
                _worldTopBar.style.paddingRight = 16f;
                _worldTopBar.style.paddingTop = 14f;
                _worldTopBar.style.paddingBottom = 14f;
                _worldTopBar.style.marginBottom = 10f;
            }

            ApplyFallbackPanelStyle(_root.Q<VisualElement>("OperationsRailPanel"), panel);
            ApplyFallbackPanelStyle(_contextInspector, panel);
            ApplyFallbackPanelStyle(_eventStripPanel, panel);

            if (_activeToolLabel != null)
            {
                _activeToolLabel.style.color = shellText;
                _activeToolLabel.style.fontSize = 16f;
            }

            if (_nextStepLabel != null)
            {
                _nextStepLabel.style.color = new Color(0.94f, 0.89f, 0.71f, 1f);
                _nextStepLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _nextStepLabel.style.fontSize = 14f;
                _nextStepLabel.style.marginBottom = 8f;
            }

            if (_contextActionHintLabel != null)
            {
                _contextActionHintLabel.style.color = new Color(0.89f, 0.93f, 0.97f, 1f);
                _contextActionHintLabel.style.fontSize = 13f;
                _contextActionHintLabel.style.marginBottom = 8f;
            }

            SetFallbackTextColor(_root, shellText);
            ApplyFallbackActionButtonStyles();
        }

        private static void ApplyFallbackPanelStyle(VisualElement panel, Color color)
        {
            if (panel == null)
            {
                return;
            }

            panel.style.backgroundColor = color;
            panel.style.paddingLeft = 14f;
            panel.style.paddingRight = 14f;
            panel.style.paddingTop = 12f;
            panel.style.paddingBottom = 12f;
            panel.style.borderTopLeftRadius = 14f;
            panel.style.borderTopRightRadius = 14f;
            panel.style.borderBottomLeftRadius = 14f;
            panel.style.borderBottomRightRadius = 14f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = new Color(0.45f, 0.52f, 0.58f, 0.35f);
            panel.style.borderBottomColor = new Color(0.45f, 0.52f, 0.58f, 0.35f);
            panel.style.borderLeftColor = new Color(0.45f, 0.52f, 0.58f, 0.35f);
            panel.style.borderRightColor = new Color(0.45f, 0.52f, 0.58f, 0.35f);
        }

        private static void SetFallbackTextColor(VisualElement root, Color color)
        {
            if (root == null)
            {
                return;
            }

            List<VisualElement> stack = new List<VisualElement> { root };
            while (stack.Count > 0)
            {
                int index = stack.Count - 1;
                VisualElement current = stack[index];
                stack.RemoveAt(index);

                if (current is TextElement textElement)
                {
                    textElement.style.color = color;
                    if (textElement.style.fontSize.value.value < 13f)
                    {
                        textElement.style.fontSize = 13f;
                    }
                }

                foreach (VisualElement child in current.Children())
                {
                    stack.Add(child);
                }
            }
        }

        private void EnsureReadableStyles()
        {
            if (_root == null)
            {
                return;
            }

            if (!_hasHudStylesheet && !_fallbackStylesApplied)
            {
                ApplyFallbackRuntimeStyles();
                return;
            }

            if (!_fallbackStylesApplied && ShouldForceFallbackFromComputedStyles())
            {
                ApplyFallbackRuntimeStyles();
                return;
            }

            if (_fallbackStylesApplied)
            {
                ApplyFallbackActionButtonStyles();
            }
        }

        private bool ShouldForceFallbackFromComputedStyles()
        {
            if (_operationsRail == null)
            {
                return false;
            }

            Button sampleButton = _operationsRail.Q<Button>(className: "action-button");
            if (sampleButton == null)
            {
                return false;
            }

            Color bg = sampleButton.resolvedStyle.backgroundColor;
            float brightness = (bg.r + bg.g + bg.b) / 3f;
            return bg.a <= 0.15f || brightness >= 0.62f;
        }

        private void ApplyFallbackActionButtonStyles()
        {
            if (_root == null)
            {
                return;
            }

            List<Button> actionButtons = _root.Query<Button>(className: "action-button").ToList();
            for (int i = 0; i < actionButtons.Count; i++)
            {
                Button button = actionButtons[i];
                bool isDisabled = button.ClassListContains("action-button--disabled");
                bool isActive = button.ClassListContains("action-button--active");
                button.style.minHeight = 56f;
                button.style.paddingLeft = 12f;
                button.style.paddingRight = 12f;
                button.style.paddingTop = 8f;
                button.style.paddingBottom = 8f;
                button.style.borderTopLeftRadius = 12f;
                button.style.borderTopRightRadius = 12f;
                button.style.borderBottomLeftRadius = 12f;
                button.style.borderBottomRightRadius = 12f;
                button.style.backgroundColor = isActive
                    ? new Color(0.32f, 0.27f, 0.17f, 0.96f)
                    : new Color(0.2f, 0.24f, 0.29f, isDisabled ? 0.72f : 0.94f);
            }

            List<Button> tabs = _root.Query<Button>(className: "inspector-tab").ToList();
            for (int i = 0; i < tabs.Count; i++)
            {
                Button tab = tabs[i];
                tab.style.minHeight = 38f;
                tab.style.paddingLeft = 12f;
                tab.style.paddingRight = 12f;
                tab.style.borderTopLeftRadius = 999f;
                tab.style.borderTopRightRadius = 999f;
                tab.style.borderBottomLeftRadius = 999f;
                tab.style.borderBottomRightRadius = 999f;
                tab.style.backgroundColor = tab.ClassListContains("inspector-tab--active")
                    ? new Color(0.31f, 0.41f, 0.48f, 0.96f)
                    : new Color(0.24f, 0.29f, 0.35f, 0.9f);
            }
        }

        private async UniTaskVoid ExecuteUiAction(GameActionEnvelope envelope, string panelId, string elementId)
        {
            GameActionResult result = await ExecuteActionAsync(envelope);
            if (result.success)
            {
                ColonyAudioService.Active?.PlaySound(ColonySoundId.UiClick);
            }
            else
            {
                ColonyAudioService.Active?.PlaySound(ColonySoundId.ErrorBlocked);
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
                    GameActionResult entityResult = await ExecuteActionAsync(_actionService.CreateEnvelope(
                        GameActionId.SelectEntity,
                        targetEntityId: units[0].entityUid.ToString(),
                        uiSource: CreateUiSource("WorldCanvas", "ClickSelectEntity")));
                    if (entityResult.success)
                    {
                        ColonyAudioService.Active?.PlaySound(ColonySoundId.SelectionChanged);
                    }

                    return;
                }

                GameActionResult cellResult = await ExecuteActionAsync(_actionService.CreateEnvelope(
                    GameActionId.SelectCell,
                    targetCell: _renderer.HoverHex.Value,
                    hasTargetCell: true,
                    uiSource: CreateUiSource("WorldCanvas", "ClickSelectCell")));
                if (cellResult.success)
                {
                    ColonyAudioService.Active?.PlaySound(ColonySoundId.SelectionChanged);
                }

                return;
            }

            if (_renderer.HoverEdge.HasValue)
            {
                GameActionResult edgeResult = await ExecuteActionAsync(_actionService.CreateEnvelope(
                    GameActionId.SelectEdge,
                    targetEdge: _renderer.HoverEdge.Value,
                    hasTargetEdge: true,
                    uiSource: CreateUiSource("WorldCanvas", "ClickSelectEdge")));
                if (edgeResult.success)
                {
                    ColonyAudioService.Active?.PlaySound(ColonySoundId.SelectionChanged);
                }
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
                container.style.opacity = 0f;
                return;
            }

            Vector3 screenPosition = UnityEngine.Camera.main.WorldToScreenPoint(state.worldPosition);
            if (screenPosition.z <= 0f)
            {
                container.style.opacity = 0f;
                return;
            }

            titleLabel.text = state.title;
            subtitleLabel.text = state.subtitle;
            container.style.opacity = 1f;
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
