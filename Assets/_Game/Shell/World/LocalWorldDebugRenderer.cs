using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ggj2026.Game.Shell.World
{
    public class LocalWorldDebugRenderer : MonoBehaviour
    {
        public enum RenderOverlayMode
        {
            Default,
            Occupancy,
            Spaces
        }

        private sealed class CellView
        {
            public Renderer Renderer;
            public Color BaseColor;
        }

        private sealed class EdgeView
        {
            public Renderer Renderer;
            public Color BaseColor;
        }

        private sealed class UnitView
        {
            public GameObject Marker;
            public Renderer Renderer;
            public Color BaseColor;
            public Vector3 BaseScale;
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
            public Quaternion StartRotation;
            public Quaternion TargetRotation;
            public float MoveStartedAt;
            public float MoveDuration;
            public float AnimationSeed;
            public JobType JobType;
            public bool TintWithStateColor;
            public readonly List<Vector3> PendingTargets = new List<Vector3>();
        }

        [SerializeField] private int visibleHexRadius = 12;
        [SerializeField] private float tileHeight = 0.12f;
        [SerializeField] private float markerHeight = 0.35f;
        [SerializeField] private bool enableMovementTween = true;
        [SerializeField] private float manualStepTweenDurationSeconds = 0.24f;
        [SerializeField] private float minTweenDurationSeconds = 0.08f;
        [SerializeField] private float maxTweenDurationSeconds = 1.2f;
        [SerializeField] private WorldVisualPrefabLibrary visualPrefabLibrary;

        private readonly Dictionary<HexCoord, CellView> _cellViews = new Dictionary<HexCoord, CellView>();
        private readonly Dictionary<HexEdgeCoord, EdgeView> _edgeViews = new Dictionary<HexEdgeCoord, EdgeView>();
        private readonly List<GameObject> _moduleMarkers = new List<GameObject>();
        private readonly List<GameObject> _itemMarkers = new List<GameObject>();
        private readonly List<GameObject> _nodeMarkers = new List<GameObject>();
        private readonly List<GameObject> _landmarkMarkers = new List<GameObject>();
        private readonly Dictionary<EntityUid, UnitView> _unitViews = new Dictionary<EntityUid, UnitView>();
        private readonly Dictionary<HexCoord, float> _constructionProgress = new Dictionary<HexCoord, float>();

        private static readonly Color ConstructionAmberColor = new Color(0.92f, 0.68f, 0.16f);
        private static readonly Dictionary<Material, Material> CompatibleMaterialCache = new Dictionary<Material, Material>();

        private Mesh _hexMesh;
        private Material _surfaceMaterial;
        private Material _markerMaterial;
        private Shader _compatibleLitShader;
        private MaterialPropertyBlock _propertyBlock;
        private Transform _cellRoot;
        private Transform _edgeRoot;
        private Transform _moduleRoot;
        private Transform _itemRoot;
        private Transform _nodeRoot;
        private Transform _landmarkRoot;
        private Transform _unitRoot;
        private GameObject _selectedCellIndicator;
        private GameObject _hoverCellIndicator;
        private GameObject _onboardingMarkerIndicator;
        private HexCoord? _onboardingMarkerCell;
        private WorldSnapshot _snapshot;
        private WorldGenerationProfile _profile;
        private LocalColonyGameplayService _gameplayService;
        private RenderOverlayMode _overlayMode;
        private float _pendingUnitInterpolationDuration;
        private HexCoord _focusHex;
        private HexCoord? _hoverHex;
        private HexCoord? _selectedHex;
        private HexEdgeCoord? _hoverEdge;
        private HexEdgeCoord? _selectedEdge;
        private EntityUid _selectedEntityUid = EntityUid.None;

        public HexCoord? HoverHex => _hoverHex;
        public HexCoord? SelectedHex => _selectedHex;
        public HexEdgeCoord? HoverEdge => _hoverEdge;
        public HexEdgeCoord? SelectedEdge => _selectedEdge;
        public EntityUid SelectedEntityUid => _selectedEntityUid;
        public RenderOverlayMode OverlayMode => _overlayMode;

        public void SetVisualPrefabLibrary(WorldVisualPrefabLibrary library)
        {
            visualPrefabLibrary = library;
            if (_snapshot != null && _profile != null && _gameplayService != null)
            {
                RebuildAll();
            }
        }

        public bool TryGetCellWorldPosition(HexCoord cell, out Vector3 worldPosition)
        {
            worldPosition = cell.ToWorld(_profile != null ? _profile.hexRadius : 1f) + Vector3.up * 0.22f;
            return _profile != null;
        }

        public bool TryGetEdgeWorldPosition(HexEdgeCoord edge, out Vector3 worldPosition)
        {
            if (_profile == null)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = edge.Midpoint(_profile.hexRadius) + Vector3.up * 0.42f;
            return true;
        }

        public bool TryGetUnitWorldPosition(EntityUid entityUid, out Vector3 worldPosition)
        {
            if (_unitViews.TryGetValue(entityUid, out UnitView view) && view != null && view.Marker != null)
            {
                worldPosition = view.Marker.transform.position + Vector3.up * 0.25f;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        public void SetOnboardingMarker(HexCoord? cell)
        {
            _onboardingMarkerCell = cell;
        }

        public void UpdateConstructionProgress(Dictionary<HexCoord, float> progressMap)
        {
            _constructionProgress.Clear();
            if (progressMap == null)
            {
                return;
            }

            foreach (KeyValuePair<HexCoord, float> entry in progressMap)
            {
                _constructionProgress[entry.Key] = Mathf.Clamp01(entry.Value);
            }

            ApplyConstructionProgressColors();
        }

        public void SetSelectedHex(HexCoord cell)
        {
            if (_selectedHex.HasValue && _selectedHex.Value == cell)
            {
                return;
            }

            SetCellVisual(_selectedHex, _hoverHex == _selectedHex, false);
            _selectedHex = cell;
            _focusHex = cell;
            SetCellVisual(_selectedHex, _hoverHex == _selectedHex, true);
        }

        public void SetSelectedEntity(EntityUid entityUid)
        {
            _selectedEntityUid = entityUid;
        }

        public void ClearSelectedEntity()
        {
            _selectedEntityUid = EntityUid.None;
        }

        public void ClearSelectedHex()
        {
            if (!_selectedHex.HasValue)
            {
                return;
            }

            SetCellVisual(_selectedHex, _hoverHex == _selectedHex, false);
            _selectedHex = null;
        }

        public void SetSelectedEdge(HexEdgeCoord edge)
        {
            if (_selectedEdge.HasValue && _selectedEdge.Value == edge)
            {
                return;
            }

            SetEdgeVisual(_selectedEdge, _hoverEdge == _selectedEdge, false);
            _selectedEdge = edge;
            _focusHex = edge.A;
            SetEdgeVisual(_selectedEdge, _hoverEdge == _selectedEdge, true);
        }

        public void ClearSelectedEdge()
        {
            if (!_selectedEdge.HasValue)
            {
                return;
            }

            SetEdgeVisual(_selectedEdge, _hoverEdge == _selectedEdge, false);
            _selectedEdge = null;
        }

        public void Initialize(WorldSnapshot snapshot, WorldGenerationProfile profile, LocalColonyGameplayService gameplayService)
        {
            if (_gameplayService != null)
            {
                _gameplayService.WorldChanged -= HandleWorldChanged;
            }

            _snapshot = snapshot;
            _profile = profile;
            _gameplayService = gameplayService;
            _focusHex = ResolveFocusHex(snapshot);

            if (_gameplayService != null)
            {
                _gameplayService.WorldChanged += HandleWorldChanged;
            }

            EnsureRenderingResources();
            RebuildAll();
        }

        public void SetOverlayMode(RenderOverlayMode overlayMode)
        {
            if (_overlayMode == overlayMode)
            {
                return;
            }

            _overlayMode = overlayMode;
            RebuildCells();
        }

        private void Update()
        {
            if (_snapshot == null || _profile == null)
            {
                return;
            }

            UpdateUnitInterpolation();
            UpdateOnboardingMarkerIndicator();

            if (Mouse.current == null || UnityEngine.Camera.main == null)
            {
                return;
            }

            if (RuntimeInputCaptureService.Active != null && RuntimeInputCaptureService.Active.IsWorldInputBlocked())
            {
                return;
            }

            UpdateHoverAndSelection();
        }

        private void OnDestroy()
        {
            if (_gameplayService != null)
            {
                _gameplayService.WorldChanged -= HandleWorldChanged;
            }
        }

        private void EnsureRenderingResources()
        {
            _cellRoot ??= CreateRoot("Cells");
            _edgeRoot ??= CreateRoot("Edges");
            _moduleRoot ??= CreateRoot("Modules");
            _itemRoot ??= CreateRoot("Items");
            _nodeRoot ??= CreateRoot("Nodes");
            _landmarkRoot ??= CreateRoot("Landmarks");
            _unitRoot ??= CreateRoot("Units");

            if (_hexMesh == null)
            {
                _hexMesh = BuildHexPrismMesh();
            }

            if (_surfaceMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _surfaceMaterial = new Material(shader);
            }

            if (_markerMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _markerMaterial = new Material(shader);
            }

            _compatibleLitShader ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _propertyBlock ??= new MaterialPropertyBlock();
            EnsureSelectionIndicators();
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void RebuildAll()
        {
            RebuildCells();
            RebuildEdges();
            RebuildModules();
            RebuildItems();
            RebuildNodes();
            RebuildLandmarks();
            RebuildUnits();
            ApplyConstructionProgressColors();
            UpdateSelectionIndicators();
        }

        private void RebuildCells()
        {
            foreach (Transform child in _cellRoot)
            {
                Destroy(child.gameObject);
            }

            _cellViews.Clear();
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                GameObject tileObject = new GameObject($"Cell_{cell.Q}_{cell.R}");
                tileObject.transform.SetParent(_cellRoot, false);
                tileObject.transform.localPosition = cell.ToWorld(_profile.hexRadius);
                tileObject.transform.localScale = new Vector3(_profile.hexRadius, tileHeight, _profile.hexRadius);

                var filter = tileObject.AddComponent<MeshFilter>();
                filter.sharedMesh = _hexMesh;

                var renderer = tileObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _surfaceMaterial;

                Color baseColor = ResolveCellColor(cell);
                ApplyColor(renderer, baseColor);
                _cellViews[cell] = new CellView { Renderer = renderer, BaseColor = baseColor };
            }
        }

        private void RebuildEdges()
        {
            foreach (Transform child in _edgeRoot)
            {
                Destroy(child.gameObject);
            }

            _edgeViews.Clear();
            var seen = new HashSet<HexEdgeCoord>();
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                    if (!seen.Add(edge))
                    {
                        continue;
                    }

                    HexEdgeState edgeState = _gameplayService.GetEdgeState(edge);
                    List<ConstructionState> pending = _gameplayService.GetConstructionAtEdge(edge);
                    if ((edgeState == null || !edgeState.isBuilt || edgeState.featureKind == EdgeFeatureKind.None) && pending.Count == 0)
                    {
                        continue;
                    }

                    Vector3 a = edge.A.ToWorld(_profile.hexRadius);
                    Vector3 b = edge.B.ToWorld(_profile.hexRadius);
                    EdgeFeatureKind featureKind = ResolveEdgeFeatureKind(edgeState, pending);
                    Vector3 axis = (b - a).normalized;
                    Vector3 wallTangent = Vector3.Cross(Vector3.up, axis).normalized;
                    Vector3 edgePosition = (a + b) * 0.5f + Vector3.up * ResolveEdgeHeight(featureKind, pending.Count > 0);
                    Quaternion edgeRotation = Quaternion.LookRotation(wallTangent, Vector3.up);
                    Vector3 fallbackScale = ResolveEdgeScale(featureKind, pending.Count > 0);
                    Color baseColor = ResolveEdgeColor(edgeState, pending.Count > 0);

                    WorldVisualPrefabLibrary.VisualPrefabDefinition edgeVisual = visualPrefabLibrary != null
                        ? visualPrefabLibrary.ResolveEdge(featureKind)
                        : default;
                    CreateFeatureMarker(edge.ToString(), _edgeRoot, edgePosition, edgeRotation, fallbackScale, edgeVisual, PrimitiveType.Cube, baseColor, true, out Renderer renderer);
                    _edgeViews[edge] = new EdgeView { Renderer = renderer, BaseColor = baseColor };
                }
            }
        }

        private void RebuildModules()
        {
            RebuildMarkers(_moduleRoot, _moduleMarkers);
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                HexCellState state = _gameplayService.GetCellState(cell);
                if (state.module == null || !state.module.isBuilt || state.module.moduleKind == ModuleKind.None)
                {
                    continue;
                }

                WorldVisualPrefabLibrary.VisualPrefabDefinition moduleVisual = visualPrefabLibrary != null
                    ? visualPrefabLibrary.ResolveModule(state.module.moduleKind)
                    : default;
                GameObject marker = CreateFeatureMarker(
                    state.module.moduleKind.ToString(),
                    _moduleRoot,
                    cell.ToWorld(_profile.hexRadius) + Vector3.up * (markerHeight * 1.1f),
                    Quaternion.identity,
                    new Vector3(0.4f, 0.4f, 0.4f),
                    moduleVisual,
                    PrimitiveType.Cube,
                    ResolveModuleColor(state.module.moduleKind),
                    true,
                    out _);
                _moduleMarkers.Add(marker);
            }
        }

        private void RebuildItems()
        {
            RebuildMarkers(_itemRoot, _itemMarkers);
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                List<ItemStackEntity> stacks = _gameplayService.GetItemStacksAtCell(cell);
                for (int i = 0; i < stacks.Count; i++)
                {
                    WorldVisualPrefabLibrary.VisualPrefabDefinition stackVisual = visualPrefabLibrary != null
                        ? visualPrefabLibrary.ResolveItemStack(stacks[i].resourceType)
                        : default;
                    GameObject marker = CreateFeatureMarker(
                        $"{stacks[i].resourceType}_{stacks[i].amount}",
                        _itemRoot,
                        cell.ToWorld(_profile.hexRadius) + Vector3.up * 0.75f + (Vector3.right * (i * 0.18f)),
                        Quaternion.identity,
                        Vector3.one * ResolveStackScale(stacks[i]),
                        stackVisual,
                        PrimitiveType.Sphere,
                        ResolveResourceColor(stacks[i].resourceType),
                        true,
                        out _);
                    _itemMarkers.Add(marker);
                }
            }
        }

        private void RebuildNodes()
        {
            RebuildMarkers(_nodeRoot, _nodeMarkers);
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                List<ResourceNodeState> nodes = _gameplayService.GetResourceNodesAtCell(cell);
                for (int i = 0; i < nodes.Count; i++)
                {
                    WorldVisualPrefabLibrary.VisualPrefabDefinition nodeVisual = visualPrefabLibrary != null
                        ? visualPrefabLibrary.ResolveResourceNode(nodes[i].resourceNodeKind)
                        : default;
                    GameObject marker = CreateFeatureMarker(
                        $"{nodes[i].resourceNodeKind}_{nodes[i].remainingYield}",
                        _nodeRoot,
                        cell.ToWorld(_profile.hexRadius) + Vector3.up * 0.6f,
                        Quaternion.identity,
                        new Vector3(0.3f, 0.6f, 0.3f),
                        nodeVisual,
                        PrimitiveType.Capsule,
                        ResolveNodeColor(nodes[i]),
                        true,
                        out _);
                    _nodeMarkers.Add(marker);
                }
            }
        }

        private void RebuildLandmarks()
        {
            RebuildMarkers(_landmarkRoot, _landmarkMarkers);
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                List<LandmarkState> landmarks = _gameplayService.GetLandmarksAtCell(cell);
                for (int i = 0; i < landmarks.Count; i++)
                {
                    if (landmarks[i].originCell != cell)
                    {
                        continue;
                    }

                    for (int j = 0; j < landmarks[i].occupiedCells.Count; j++)
                    {
                        HexCoord occupiedCell = landmarks[i].occupiedCells[j];
                        WorldVisualPrefabLibrary.VisualPrefabDefinition landmarkVisual = visualPrefabLibrary != null
                            ? visualPrefabLibrary.ResolveLandmark(landmarks[i].landmarkKind)
                            : default;
                        GameObject marker = CreateFeatureMarker(
                            landmarks[i].landmarkKind.ToString(),
                            _landmarkRoot,
                            occupiedCell.ToWorld(_profile.hexRadius) + Vector3.up * 0.9f,
                            Quaternion.identity,
                            new Vector3(0.55f, 0.7f, 0.55f),
                            landmarkVisual,
                            PrimitiveType.Cylinder,
                            ResolveLandmarkColor(landmarks[i].landmarkKind),
                            true,
                            out _);
                        _landmarkMarkers.Add(marker);
                    }
                }
            }
        }

        private void RebuildUnits()
        {
            float moveDuration = ResolveMoveInterpolationDurationSeconds();
            var activeUnitIds = new HashSet<EntityUid>();
            foreach (HexCoord cell in EnumerateHexes(_focusHex, visibleHexRadius))
            {
                List<UnitSnapshot> units = _gameplayService.GetUnitsAtCell(cell);
                for (int i = 0; i < units.Count; i++)
                {
                    UnitSnapshot unit = units[i];
                    activeUnitIds.Add(unit.entityUid);
                    Vector3 targetPosition = cell.ToWorld(_profile.hexRadius) + Vector3.up * 1.15f + ResolveUnitCellOffset(unit.entityUid);
                    Color color = ResolveUnitColor(unit);

                    if (!_unitViews.TryGetValue(unit.entityUid, out UnitView view) || view.Marker == null)
                    {
                        WorldVisualPrefabLibrary.VisualPrefabDefinition unitVisual = visualPrefabLibrary != null
                            ? visualPrefabLibrary.ResolveUnit(unit.unitRole)
                            : default;
                        bool tintUnit = unitVisual.prefab == null || unitVisual.tintWithStateColor;
                        GameObject marker = CreateFeatureMarker(
                            $"{unit.unitRole}_{unit.entityUid}",
                            _unitRoot,
                            targetPosition,
                            Quaternion.identity,
                            Vector3.one * 0.28f,
                            unitVisual,
                            PrimitiveType.Sphere,
                            color,
                            true,
                            out Renderer renderer);

                        view = new UnitView
                        {
                            Marker = marker,
                            Renderer = renderer,
                            BaseColor = color,
                            BaseScale = marker.transform.localScale,
                            StartPosition = targetPosition,
                            TargetPosition = targetPosition,
                            StartRotation = marker.transform.localRotation,
                            TargetRotation = marker.transform.localRotation,
                            MoveStartedAt = Time.unscaledTime,
                            MoveDuration = 0f,
                            AnimationSeed = ((unit.entityUid.Value & 255UL) / 255f) * Mathf.PI * 2f,
                            JobType = unit.currentJobType,
                            TintWithStateColor = tintUnit
                        };
                        _unitViews[unit.entityUid] = view;
                        view.Marker.transform.localScale = view.BaseScale;
                        continue;
                    }

                    if (view.BaseColor != color)
                    {
                        view.BaseColor = color;
                    }

                    view.JobType = unit.currentJobType;

                    if ((view.TargetPosition - targetPosition).sqrMagnitude > 0.0001f)
                    {
                        QueueUnitMove(view, targetPosition, moveDuration);
                    }
                    else if (view.MoveDuration <= 0f)
                    {
                        view.StartPosition = targetPosition;
                        view.TargetPosition = targetPosition;
                        view.PendingTargets.Clear();
                        view.Marker.transform.localPosition = targetPosition;
                    }
                }
            }

            var staleUnitIds = new List<EntityUid>();
            foreach (KeyValuePair<EntityUid, UnitView> pair in _unitViews)
            {
                if (!activeUnitIds.Contains(pair.Key))
                {
                    if (pair.Value.Marker != null)
                    {
                        Destroy(pair.Value.Marker);
                    }

                    staleUnitIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleUnitIds.Count; i++)
            {
                _unitViews.Remove(staleUnitIds[i]);
            }

            if (_selectedEntityUid != EntityUid.None && !_unitViews.ContainsKey(_selectedEntityUid))
            {
                _selectedEntityUid = EntityUid.None;
            }

            _pendingUnitInterpolationDuration = 0f;
        }

        private static void RebuildMarkers(Transform root, List<GameObject> markers)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    Destroy(markers[i]);
                }
            }

            markers.Clear();
            foreach (Transform child in root)
            {
                Destroy(child.gameObject);
            }
        }

        private void PrepareMarker(GameObject marker, Color color)
        {
            StripColliders(marker);

            if (marker.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = _markerMaterial;
                ApplyColor(renderer, color);
            }
        }

        private GameObject CreateFeatureMarker(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 fallbackScale,
            WorldVisualPrefabLibrary.VisualPrefabDefinition visualDefinition,
            PrimitiveType fallbackPrimitive,
            Color tintColor,
            bool stripColliders,
            out Renderer primaryRenderer)
        {
            bool hasPrefab = visualDefinition.prefab != null;
            GameObject marker = hasPrefab
                ? Instantiate(visualDefinition.prefab)
                : GameObject.CreatePrimitive(fallbackPrimitive);

            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition + visualDefinition.localOffset;
            marker.transform.localRotation = localRotation * Quaternion.Euler(visualDefinition.localEuler);
            if (hasPrefab)
            {
                marker.transform.localScale = Vector3.one;
                EnsureCompatiblePrefabMaterials(marker);
                if (visualDefinition.useCustomScale && visualDefinition.localScale.sqrMagnitude > 0.0001f)
                {
                    marker.transform.localScale = visualDefinition.localScale;
                }
                else
                {
                    AutoFitPrefabScaleToTarget(marker, fallbackScale);
                }
            }
            else
            {
                marker.transform.localScale = fallbackScale;
            }

            if (stripColliders)
            {
                StripColliders(marker);
            }

            primaryRenderer = FindPrimaryRenderer(marker);
            if (primaryRenderer != null && (!hasPrefab || visualDefinition.tintWithStateColor))
            {
                primaryRenderer.sharedMaterial = _markerMaterial;
                ApplyColor(primaryRenderer, tintColor);
            }

            return marker;
        }

        private void AutoFitPrefabScaleToTarget(GameObject marker, Vector3 targetSize)
        {
            if (marker == null)
            {
                return;
            }

            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                marker.transform.localScale = targetSize;
                return;
            }

            bool hasBounds = false;
            Bounds combinedBounds = new Bounds(marker.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds)
            {
                marker.transform.localScale = targetSize;
                return;
            }

            Vector3 sourceSize = combinedBounds.size;
            if (sourceSize.x <= 0.0001f || sourceSize.y <= 0.0001f || sourceSize.z <= 0.0001f)
            {
                marker.transform.localScale = targetSize;
                return;
            }

            float fitFactorX = Mathf.Max(0.0001f, targetSize.x) / sourceSize.x;
            float fitFactorY = Mathf.Max(0.0001f, targetSize.y) / sourceSize.y;
            float fitFactorZ = Mathf.Max(0.0001f, targetSize.z) / sourceSize.z;
            float fitFactor = Mathf.Clamp(Mathf.Min(fitFactorX, Mathf.Min(fitFactorY, fitFactorZ)), 0.001f, 100f);
            marker.transform.localScale = Vector3.one * fitFactor;
        }

        private void EnsureCompatiblePrefabMaterials(GameObject marker)
        {
            if (marker == null || _compatibleLitShader == null)
            {
                return;
            }

            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material compatible = GetCompatibleMaterial(materials[j]);
                    if (compatible != null && compatible != materials[j])
                    {
                        materials[j] = compatible;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private Material GetCompatibleMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (CompatibleMaterialCache.TryGetValue(source, out Material cached))
            {
                return cached;
            }

            string shaderName = source.shader != null ? source.shader.name : string.Empty;
            bool unsupported = source.shader == null ||
                               shaderName == "Hidden/InternalErrorShader" ||
                               shaderName.StartsWith("Toon/") ||
                               shaderName.StartsWith("Legacy Shaders/") ||
                               shaderName.StartsWith("Particles/") ||
                               shaderName.Contains("Projector") ||
                               shaderName.Contains("Autodesk") ||
                               shaderName == "Standard";
            if (!unsupported)
            {
                CompatibleMaterialCache[source] = source;
                return source;
            }

            var repaired = new Material(_compatibleLitShader) { name = source.name + "_URPCompat" };
            Texture sourceTexture = source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex")
                    ? source.GetTexture("_MainTex")
                    : null;
            Color sourceColor = source.HasProperty("_BaseColor")
                ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color")
                    ? source.GetColor("_Color")
                    : Color.white;

            if (repaired.HasProperty("_BaseMap"))
            {
                repaired.SetTexture("_BaseMap", sourceTexture);
            }

            if (repaired.HasProperty("_BaseColor"))
            {
                repaired.SetColor("_BaseColor", sourceColor);
            }

            CompatibleMaterialCache[source] = repaired;
            return repaired;
        }

        private static Renderer FindPrimaryRenderer(GameObject marker)
        {
            if (marker == null)
            {
                return null;
            }

            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            return renderers.Length > 0 ? renderers[0] : null;
        }

        private static void StripColliders(GameObject marker)
        {
            if (marker == null)
            {
                return;
            }

            Collider[] colliders = marker.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }
        }

        private float ResolveMoveInterpolationDurationSeconds()
        {
            if (!enableMovementTween)
            {
                return 0f;
            }

            float duration = _pendingUnitInterpolationDuration;
            if (duration <= 0f && RealTimeSimulationController.Active != null && RealTimeSimulationController.Active.IsRunning)
            {
                duration = RealTimeSimulationController.Active.EffectiveTickDurationSeconds * 0.92f;
            }

            if (duration <= 0f)
            {
                duration = manualStepTweenDurationSeconds;
            }

            return Mathf.Clamp(duration, minTweenDurationSeconds, maxTweenDurationSeconds);
        }

        private void QueueUnitMove(UnitView view, Vector3 targetPosition, float moveDuration)
        {
            if (!enableMovementTween || moveDuration <= 0f)
            {
                view.PendingTargets.Clear();
                view.MoveDuration = 0f;
                view.StartPosition = targetPosition;
                view.TargetPosition = targetPosition;
                view.Marker.transform.localPosition = targetPosition;
                return;
            }

            if (view.MoveDuration <= 0f && view.PendingTargets.Count == 0)
            {
                StartUnitMove(view, targetPosition, moveDuration);
                return;
            }

            Vector3 latestQueuedTarget = view.PendingTargets.Count > 0
                ? view.PendingTargets[view.PendingTargets.Count - 1]
                : view.TargetPosition;
            if ((latestQueuedTarget - targetPosition).sqrMagnitude > 0.0001f)
            {
                view.PendingTargets.Add(targetPosition);
            }
        }

        private static Vector3 ResolveUnitCellOffset(EntityUid entityUid)
        {
            ulong seed = entityUid.Value;
            float ring = 0.06f + ((seed % 3UL) * 0.04f);
            float angle = (seed % 360UL) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring);
        }

        private static void StartUnitMove(UnitView view, Vector3 targetPosition, float moveDuration)
        {
            Vector3 currentPosition = view.Marker.transform.localPosition;
            Vector3 moveVector = targetPosition - currentPosition;
            view.StartPosition = currentPosition;
            view.TargetPosition = targetPosition;
            view.MoveStartedAt = Time.unscaledTime;
            view.MoveDuration = Mathf.Max(0.02f, moveDuration);

            view.StartRotation = view.Marker.transform.localRotation;
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                view.TargetRotation = Quaternion.LookRotation(moveVector.normalized, Vector3.up);
            }
            else
            {
                view.TargetRotation = view.StartRotation;
            }
        }

        private void UpdateHoverAndSelection()
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter))
            {
                return;
            }

            Vector3 worldPoint = ray.GetPoint(enter);
            HexCoord hoveredCell = HexCoord.FromWorld(worldPoint, _profile.hexRadius);
            HexEdgeCoord hoveredEdge = ResolveNearestEdge(hoveredCell, worldPoint);

            if (_hoverHex != hoveredCell)
            {
                SetCellVisual(_hoverHex, false, false);
                _hoverHex = hoveredCell;
                SetCellVisual(_hoverHex, true, _selectedHex == hoveredCell);
            }

            if (_hoverEdge != hoveredEdge)
            {
                SetEdgeVisual(_hoverEdge, false, false);
                _hoverEdge = hoveredEdge;
                SetEdgeVisual(_hoverEdge, true, _selectedEdge == hoveredEdge);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                UpdateSelectionIndicators();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                UpdateSelectionIndicators();
            }
        }

        private HexEdgeCoord ResolveNearestEdge(HexCoord cell, Vector3 worldPoint)
        {
            HexEdgeCoord bestEdge = HexEdgeCoord.FromCellAndDirection(cell, 0);
            float bestDistance = float.MaxValue;
            for (int direction = 0; direction < 6; direction++)
            {
                HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                float distance = Vector3.Distance(edge.Midpoint(_profile.hexRadius), worldPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestEdge = edge;
                }
            }

            return bestEdge;
        }

        private void SetCellVisual(HexCoord? cell, bool hovered, bool selected)
        {
            if (!cell.HasValue || !_cellViews.TryGetValue(cell.Value, out CellView view))
            {
                return;
            }

            Color color = view.BaseColor;
            if (selected)
            {
                color = Color.Lerp(color, Color.white, 0.55f);
            }
            else if (hovered)
            {
                color = Color.Lerp(color, Color.white, 0.25f);
            }

            ApplyColor(view.Renderer, color);
            UpdateSelectionIndicators();
        }

        private void SetEdgeVisual(HexEdgeCoord? edge, bool hovered, bool selected)
        {
            if (!edge.HasValue || !_edgeViews.TryGetValue(edge.Value, out EdgeView view))
            {
                return;
            }

            Color color = view.BaseColor;
            if (selected)
            {
                color = Color.Lerp(color, Color.white, 0.62f);
            }
            else if (hovered)
            {
                color = Color.Lerp(color, Color.white, 0.34f);
            }

            ApplyColor(view.Renderer, color);
            UpdateSelectionIndicators();
        }

        private void HandleWorldChanged(WorldSnapshot snapshot)
        {
            if (RealTimeSimulationController.Active != null &&
                RealTimeSimulationController.Active.TryGetInterpolationDurationForTick(snapshot.simulationTick, out float interpolationDuration))
            {
                _pendingUnitInterpolationDuration = interpolationDuration;
            }
            else
            {
                _pendingUnitInterpolationDuration = 0f;
            }

            _snapshot = snapshot;
            _focusHex = ResolveFocusHex(snapshot);
            RebuildAll();
        }

        private void EnsureSelectionIndicators()
        {
            _selectedCellIndicator ??= CreateIndicator("SelectedCellIndicator", new Color(0.98f, 0.92f, 0.44f, 0.86f), new Vector3(_profile.hexRadius * 1.2f, 0.025f, _profile.hexRadius * 1.2f));
            _hoverCellIndicator ??= CreateIndicator("HoverCellIndicator", new Color(0.86f, 0.95f, 1f, 0.32f), new Vector3(_profile.hexRadius * 1.08f, 0.02f, _profile.hexRadius * 1.08f));
            _onboardingMarkerIndicator ??= CreateIndicator("OnboardingMarkerIndicator", new Color(0.30f, 0.82f, 0.96f, 0.72f), new Vector3(_profile.hexRadius * 1.5f, 0.018f, _profile.hexRadius * 1.5f));
        }

        private GameObject CreateIndicator(string name, Color color, Vector3 scale)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = name;
            indicator.transform.SetParent(transform, false);
            indicator.transform.localScale = scale;
            indicator.transform.localPosition = new Vector3(0f, -100f, 0f);
            PrepareMarker(indicator, color);
            return indicator;
        }

        private void UpdateSelectionIndicators()
        {
            UpdateCellIndicator(_hoverCellIndicator, _hoverHex, _selectedHex.HasValue && _hoverHex.HasValue && _selectedHex.Value == _hoverHex.Value);
            UpdateCellIndicator(_selectedCellIndicator, _selectedHex, true);
            UpdateOnboardingMarkerIndicator();
        }

        private void UpdateCellIndicator(GameObject indicator, HexCoord? cell, bool emphasized)
        {
            if (indicator == null)
            {
                return;
            }

            if (!cell.HasValue || _profile == null)
            {
                indicator.transform.localPosition = new Vector3(0f, -100f, 0f);
                return;
            }

            indicator.transform.localPosition = cell.Value.ToWorld(_profile.hexRadius) + Vector3.up * 0.02f;
            float radiusScale = emphasized ? 1.26f : 1.08f;
            float heightScale = emphasized ? 0.026f : 0.018f;
            indicator.transform.localScale = new Vector3(_profile.hexRadius * radiusScale, heightScale, _profile.hexRadius * radiusScale);
        }

        private void UpdateOnboardingMarkerIndicator()
        {
            if (_onboardingMarkerIndicator == null)
            {
                return;
            }

            if (!_onboardingMarkerCell.HasValue || _profile == null)
            {
                _onboardingMarkerIndicator.transform.localPosition = new Vector3(0f, -100f, 0f);
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.18f;
            float radiusScale = 1.5f * pulse;
            _onboardingMarkerIndicator.transform.localPosition = _onboardingMarkerCell.Value.ToWorld(_profile.hexRadius) + Vector3.up * 0.035f;
            _onboardingMarkerIndicator.transform.localScale = new Vector3(_profile.hexRadius * radiusScale, 0.018f, _profile.hexRadius * radiusScale);

            float alphaWave = 0.50f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.22f;
            ApplyColor(_onboardingMarkerIndicator.GetComponent<Renderer>(), new Color(0.30f, 0.82f, 0.96f, alphaWave));
        }

        private void ApplyConstructionProgressColors()
        {
            foreach (KeyValuePair<HexCoord, float> entry in _constructionProgress)
            {
                if (!_cellViews.TryGetValue(entry.Key, out CellView view))
                {
                    continue;
                }

                Color tinted = Color.Lerp(ConstructionAmberColor, view.BaseColor, entry.Value);
                ApplyColor(view.Renderer, tinted);
            }
        }

        private void UpdateUnitInterpolation()
        {
            foreach (KeyValuePair<EntityUid, UnitView> pair in _unitViews)
            {
                UnitView view = pair.Value;
                if (view == null || view.Marker == null)
                {
                    continue;
                }

                float time = Time.unscaledTime + view.AnimationSeed;
                bool isMoving = view.MoveDuration > 0f;
                bool isWorking = IsWorkingJob(view.JobType);
                bool isSelected = pair.Key == _selectedEntityUid;

                Vector3 basePosition;
                Quaternion baseRotation = view.Marker.transform.localRotation;
                if (isMoving)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - view.MoveStartedAt) / view.MoveDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    float strideWave = Mathf.Sin((smoothT * Mathf.PI * 4f) + view.AnimationSeed);
                    basePosition = Vector3.Lerp(view.StartPosition, view.TargetPosition, smoothT);
                    basePosition.y += Mathf.Sin(smoothT * Mathf.PI) * 0.16f + Mathf.Abs(strideWave) * 0.06f;
                    baseRotation = Quaternion.Slerp(view.StartRotation, view.TargetRotation, smoothT) *
                                   Quaternion.Euler(strideWave * 2.2f, 0f, strideWave * 6.2f);
                    view.Marker.transform.localScale = new Vector3(view.BaseScale.x * 1.05f, view.BaseScale.y * 0.95f, view.BaseScale.z * 1.05f);
                    if (t >= 1f)
                    {
                        view.MoveDuration = 0f;
                        view.StartPosition = view.TargetPosition;
                        view.Marker.transform.localPosition = view.TargetPosition;
                        view.Marker.transform.localRotation = view.TargetRotation;

                        if (view.PendingTargets.Count > 0)
                        {
                            Vector3 nextTarget = view.PendingTargets[0];
                            view.PendingTargets.RemoveAt(0);
                            StartUnitMove(view, nextTarget, ResolveMoveInterpolationDurationSeconds());
                        }
                    }
                }
                else
                {
                    float idleBob = Mathf.Sin(time * 1.7f) * 0.025f;
                    float workBob = isWorking ? Mathf.Abs(Mathf.Sin(time * 5.5f)) * 0.08f : 0f;
                    basePosition = view.TargetPosition + Vector3.up * (idleBob + workBob);
                    float pulse = isWorking ? 1f + (Mathf.Sin(time * 6f) * 0.08f) : 1f + (Mathf.Sin(time * 2.2f) * 0.02f);
                    view.Marker.transform.localScale = view.BaseScale * pulse;
                    baseRotation = Quaternion.Slerp(view.Marker.transform.localRotation, view.TargetRotation, 0.15f);
                }

                if (isSelected)
                {
                    basePosition += Vector3.up * 0.08f;
                    view.Marker.transform.localScale *= 1.28f;
                }

                view.Marker.transform.localPosition = basePosition;
                view.Marker.transform.localRotation = baseRotation;

                if (view.TintWithStateColor && view.Renderer != null)
                {
                    ApplyColor(view.Renderer, ResolveAnimatedUnitColor(view.BaseColor, isWorking, isMoving, isSelected, time));
                }
            }
        }

        private static bool IsWorkingJob(JobType jobType)
        {
            return jobType == JobType.HarvestSource ||
                   jobType == JobType.HaulToStaging ||
                   jobType == JobType.HaulToStorage ||
                   jobType == JobType.HaulToConsumer ||
                   jobType == JobType.BuildFloor ||
                   jobType == JobType.BuildEdge ||
                   jobType == JobType.InstallModule ||
                   jobType == JobType.PlaceLandmark ||
                   jobType == JobType.OperateModule;
        }

        private static Color ResolveAnimatedUnitColor(Color baseColor, bool isWorking, bool isMoving, bool isSelected, float time)
        {
            if (isSelected)
            {
                return Color.Lerp(baseColor, Color.white, 0.32f + (Mathf.Abs(Mathf.Sin(time * 5f)) * 0.22f));
            }

            if (isWorking)
            {
                return Color.Lerp(baseColor, Color.white, 0.14f + (Mathf.Abs(Mathf.Sin(time * 6f)) * 0.16f));
            }

            if (isMoving)
            {
                return Color.Lerp(baseColor, Color.white, 0.12f);
            }

            return Color.Lerp(baseColor, Color.white, 0.03f + (Mathf.Sin(time * 2f) * 0.02f));
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private Color ResolveCellColor(HexCoord cell)
        {
            if (_overlayMode == RenderOverlayMode.Occupancy)
            {
                return ResolveOccupancyOverlayColor(cell);
            }

            if (_overlayMode == RenderOverlayMode.Spaces)
            {
                return ResolveSpaceOverlayColor(cell);
            }

            HexCellState state = _gameplayService.GetCellState(cell);
            if (state.floor != null && state.floor.isBuilt)
            {
                return state.floor.purposeKind switch
                {
                    FloorPurposeKind.Residential => new Color(0.82f, 0.80f, 0.66f),
                    FloorPurposeKind.Storage => new Color(0.63f, 0.48f, 0.28f),
                    FloorPurposeKind.Workshop => new Color(0.61f, 0.59f, 0.68f),
                    FloorPurposeKind.Weaponry => new Color(0.62f, 0.27f, 0.24f),
                    FloorPurposeKind.Barracks => new Color(0.46f, 0.36f, 0.28f),
                    FloorPurposeKind.Roadway => new Color(0.42f, 0.42f, 0.46f),
                    FloorPurposeKind.Farmland => new Color(0.40f, 0.62f, 0.24f),
                    FloorPurposeKind.Utility => new Color(0.29f, 0.51f, 0.64f),
                    _ => ResolveBiomeColor(cell)
                };
            }

            if (_gameplayService.GetConstructionAtCell(cell).Count > 0)
            {
                return new Color(0.18f, 0.68f, 0.78f);
            }

            return ResolveBiomeColor(cell);
        }

        private Color ResolveOccupancyOverlayColor(HexCoord cell)
        {
            OccupancyEvaluation occupancy = _gameplayService.GetOccupancy(cell);
            if (occupancy.isHardBlocked)
            {
                return new Color(0.78f, 0.22f, 0.22f);
            }

            float capacityRatio = occupancy.availableGroundSmallCapacity > 0
                ? Mathf.Clamp01((float)occupancy.usedGroundSmallCapacity / occupancy.availableGroundSmallCapacity)
                : 1f;
            float moveRatio = Mathf.Clamp01((occupancy.moveCost - 0.35f) / 1.4f);
            return Color.Lerp(new Color(0.22f, 0.68f, 0.30f), new Color(0.92f, 0.62f, 0.18f), Mathf.Max(capacityRatio, moveRatio));
        }

        private Color ResolveSpaceOverlayColor(HexCoord cell)
        {
            SpaceRegionState space = _gameplayService.GetSpaceAtCell(cell);
            if (space == null)
            {
                return ResolveBiomeColor(cell);
            }

            return space.activationState switch
            {
                SpaceActivationState.OpenActive => new Color(0.28f, 0.62f, 0.82f),
                SpaceActivationState.ShelteredActive => new Color(0.85f, 0.78f, 0.28f),
                SpaceActivationState.Blocked => new Color(0.58f, 0.24f, 0.24f),
                _ => ResolveBiomeColor(cell)
            };
        }

        private Color ResolveBiomeColor(HexCoord cell)
        {
            ChunkCoord chunkCoord = ChunkCoord.FromHex(cell, _profile.chunkSize);
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                if (_snapshot.chunks[i].chunkCoord != chunkCoord)
                {
                    continue;
                }

                return ResolveBiomeColor(_snapshot.chunks[i].dominantBiome);
            }

            return ResolveBiomeColor(BiomeKind.Plains);
        }

        private static Color ResolveBiomeColor(BiomeKind biomeKind)
        {
            return biomeKind switch
            {
                BiomeKind.Forest => new Color(0.18f, 0.44f, 0.22f),
                BiomeKind.Hills => new Color(0.47f, 0.42f, 0.32f),
                BiomeKind.Wetlands => new Color(0.14f, 0.39f, 0.44f),
                BiomeKind.Desert => new Color(0.72f, 0.63f, 0.31f),
                BiomeKind.Tundra => new Color(0.64f, 0.71f, 0.76f),
                _ => new Color(0.38f, 0.52f, 0.25f)
            };
        }

        private static Color ResolveEdgeColor(HexEdgeState edgeState, bool hasPendingConstruction)
        {
            if (hasPendingConstruction && (edgeState == null || !edgeState.isBuilt))
            {
                return new Color(0.22f, 0.74f, 0.83f);
            }

            if (edgeState == null || !edgeState.isBuilt)
            {
                return new Color(0.25f, 0.25f, 0.25f);
            }

            return edgeState.featureKind switch
            {
                EdgeFeatureKind.Wall => new Color(0.70f, 0.72f, 0.78f),
                EdgeFeatureKind.Door => new Color(0.93f, 0.67f, 0.25f),
                EdgeFeatureKind.Gate => new Color(0.84f, 0.71f, 0.34f),
                _ => new Color(0.70f, 0.70f, 0.70f)
            };
        }

        private static EdgeFeatureKind ResolveEdgeFeatureKind(HexEdgeState edgeState, List<ConstructionState> pending)
        {
            if (edgeState != null && edgeState.isBuilt)
            {
                return edgeState.featureKind;
            }

            if (pending != null)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].blueprint.featureKind == ConstructionFeatureKind.Edge)
                    {
                        return pending[i].blueprint.edgeFeatureKind;
                    }
                }
            }

            return EdgeFeatureKind.None;
        }

        private Vector3 ResolveEdgeScale(EdgeFeatureKind featureKind, bool hasPendingConstruction)
        {
            float length = _profile != null ? _profile.hexRadius * 1.18f : 1.1f;
            if (hasPendingConstruction && featureKind == EdgeFeatureKind.None)
            {
                return new Vector3(0.10f, 0.24f, length);
            }

            return featureKind switch
            {
                EdgeFeatureKind.Door => new Vector3(0.26f, 0.22f, length * 0.40f),
                EdgeFeatureKind.Gate => new Vector3(0.28f, 0.42f, length * 0.72f),
                _ => new Vector3(0.18f, 0.58f, length * 0.92f)
            };
        }

        private static float ResolveEdgeHeight(EdgeFeatureKind featureKind, bool hasPendingConstruction)
        {
            if (hasPendingConstruction && featureKind == EdgeFeatureKind.None)
            {
                return 0.15f;
            }

            return featureKind switch
            {
                EdgeFeatureKind.Door => 0.18f,
                EdgeFeatureKind.Gate => 0.22f,
                _ => 0.26f
            };
        }

        private static Color ResolveModuleColor(ModuleKind moduleKind)
        {
            return moduleKind switch
            {
                ModuleKind.Bed => new Color(0.87f, 0.82f, 0.73f),
                ModuleKind.Rack => new Color(0.64f, 0.42f, 0.23f),
                ModuleKind.Bin => new Color(0.58f, 0.38f, 0.19f),
                ModuleKind.Pallet => new Color(0.72f, 0.52f, 0.26f),
                ModuleKind.Workbench => new Color(0.56f, 0.66f, 0.86f),
                ModuleKind.WeaponBench => new Color(0.78f, 0.30f, 0.30f),
                ModuleKind.Bunk => new Color(0.48f, 0.36f, 0.22f),
                ModuleKind.CommandPost => new Color(0.25f, 0.56f, 0.76f),
                ModuleKind.DrillPost => new Color(0.92f, 0.72f, 0.22f),
                _ => Color.white
            };
        }

        private static Color ResolveResourceColor(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Wood => new Color(0.52f, 0.33f, 0.16f),
                ResourceType.Stone => new Color(0.62f, 0.62f, 0.66f),
                ResourceType.Ore => new Color(0.24f, 0.40f, 0.64f),
                ResourceType.Metal => new Color(0.76f, 0.76f, 0.82f),
                ResourceType.Food => new Color(0.44f, 0.72f, 0.28f),
                ResourceType.Weapons => new Color(0.78f, 0.30f, 0.30f),
                ResourceType.Supply => new Color(0.84f, 0.64f, 0.28f),
                _ => Color.white
            };
        }

        private static float ResolveStackScale(ItemStackEntity stack)
        {
            return stack.bulkClass switch
            {
                BulkClass.LooseSmall => 0.2f,
                BulkClass.PileMedium => 0.3f,
                BulkClass.PileLarge => 0.42f,
                _ => 0.24f
            };
        }

        private static Color ResolveNodeColor(ResourceNodeState node)
        {
            return node.resourceNodeKind switch
            {
                ResourceNodeKind.Tree => new Color(0.18f, 0.46f, 0.18f),
                ResourceNodeKind.StoneOutcrop => new Color(0.62f, 0.62f, 0.70f),
                ResourceNodeKind.OreSeam => new Color(0.22f, 0.40f, 0.68f),
                ResourceNodeKind.FertileSoil => new Color(0.48f, 0.58f, 0.18f),
                _ => Color.white
            };
        }

        private static Color ResolveLandmarkColor(LandmarkKind landmarkKind)
        {
            return landmarkKind switch
            {
                LandmarkKind.TownCenterCore => new Color(0.90f, 0.75f, 0.24f),
                LandmarkKind.Tower => new Color(0.72f, 0.72f, 0.78f),
                LandmarkKind.Gatehouse => new Color(0.84f, 0.66f, 0.30f),
                _ => Color.white
            };
        }

        private static Color ResolveUnitColor(UnitSnapshot unit)
        {
            Color roleColor = unit.unitRole switch
            {
                UnitRole.Builder => new Color(0.94f, 0.63f, 0.24f),
                UnitRole.Hauler => new Color(0.82f, 0.54f, 0.18f),
                UnitRole.Worker => new Color(0.30f, 0.74f, 0.38f),
                UnitRole.Crafter => new Color(0.42f, 0.62f, 0.84f),
                UnitRole.Soldier => new Color(0.84f, 0.30f, 0.30f),
                _ => new Color(0.86f, 0.86f, 0.86f)
            };

            return unit.currentJobType switch
            {
                JobType.HarvestSource => Color.Lerp(roleColor, Color.green, 0.25f),
                JobType.HaulToStaging => Color.Lerp(roleColor, Color.yellow, 0.25f),
                JobType.BuildFloor or JobType.BuildEdge or JobType.InstallModule or JobType.PlaceLandmark => Color.Lerp(roleColor, Color.white, 0.2f),
                _ => roleColor
            };
        }

        private static HexCoord ResolveFocusHex(WorldSnapshot snapshot)
        {
            return snapshot?.colonies != null && snapshot.colonies.Count > 0
                ? snapshot.colonies[0].center
                : HexCoord.Zero;
        }

        private static IEnumerable<HexCoord> EnumerateHexes(HexCoord center, int radius)
        {
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-radius, -q - radius);
                int rMax = Mathf.Min(radius, -q + radius);
                for (int r = rMin; r <= rMax; r++)
                {
                    yield return new HexCoord(center.Q + q, center.R + r);
                }
            }
        }

        private static Mesh BuildHexPrismMesh()
        {
            const float halfHeight = 0.5f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            Vector3[] top = new Vector3[6];
            Vector3[] bottom = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * ((60f * i) - 30f);
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                top[i] = new Vector3(x, halfHeight, z);
                bottom[i] = new Vector3(x, -halfHeight, z);
            }

            int topCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, halfHeight, 0f));
            for (int i = 0; i < 6; i++)
            {
                vertices.Add(top[i]);
            }

            for (int i = 0; i < 6; i++)
            {
                int current = topCenterIndex + 1 + i;
                int next = topCenterIndex + 1 + ((i + 1) % 6);
                triangles.Add(topCenterIndex);
                triangles.Add(next);
                triangles.Add(current);
            }

            int bottomCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, -halfHeight, 0f));
            for (int i = 0; i < 6; i++)
            {
                vertices.Add(bottom[i]);
            }

            for (int i = 0; i < 6; i++)
            {
                int current = bottomCenterIndex + 1 + i;
                int next = bottomCenterIndex + 1 + ((i + 1) % 6);
                triangles.Add(bottomCenterIndex);
                triangles.Add(current);
                triangles.Add(next);
            }

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;

                int sideStart = vertices.Count;
                vertices.Add(top[i]);
                vertices.Add(top[next]);
                vertices.Add(bottom[i]);
                vertices.Add(bottom[next]);

                triangles.Add(sideStart);
                triangles.Add(sideStart + 1);
                triangles.Add(sideStart + 2);

                triangles.Add(sideStart + 1);
                triangles.Add(sideStart + 3);
                triangles.Add(sideStart + 2);
            }

            var mesh = new Mesh { name = "HexPrism" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
