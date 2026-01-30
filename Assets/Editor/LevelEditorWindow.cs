using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using BreakingHue.Core;
using BreakingHue.Level.Data;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Unity Editor window for creating and editing levels.
    /// Provides a visual grid-based editor with layer support, undo/redo,
    /// and non-destructive editing.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        #region Constants
        
        private const int GridSize = 32;
        private const float CellSize = 16f;
        private const float ToolbarHeight = 60f;
        private const float LayerPanelWidth = 220f;
        private const float PropertiesPanelWidth = 280f;
        private const int MaxUndoSteps = 100;
        
        #endregion
        
        #region State
        
        // Level data
        private LevelData _currentLevel;
        private LevelDataSnapshot _workingCopy;
        private bool _hasUnsavedChanges;
        
        // View state
        private Vector2 _scrollPosition;
        private Vector2 _propertiesScrollPosition;
        private Vector2 _gridOffset = Vector2.zero;
        private float _zoom = 1f;
        
        // Tool state
        private EditorLayer _currentLayer = EditorLayer.Ground;
        private EditorTool _currentTool = EditorTool.Paint;
        private ColorType _selectedColor = ColorType.Red;
        private bool _isCheckpoint;
        
        // Layer visibility
        private bool[] _layerVisibility = new bool[9] { true, true, true, true, true, true, true, true, true };
        
        // Bot path editing
        private List<Vector2Int> _currentBotPath = new List<Vector2Int>();
        private string _currentBotId;
        private PathMode _currentPathMode = PathMode.Loop;
        private BotDataSnapshot _selectedBotForPath;
        
        // Portal linking
        private PortalDataSnapshot _selectedPortalA;
        private PortalDataSnapshot _selectedPortalB;
        
        // Selection state
        private List<SelectedItem> _selection = new List<SelectedItem>();
        private bool _isDraggingSelection;
        private Vector2Int _dragStartPos;
        private Vector2Int _dragCurrentPos;
        
        // Fill tool state
        private bool _isFilling;
        private Vector2Int _fillStartPos;
        private Vector2Int _fillEndPos;
        
        // Undo/Redo
        private Stack<LevelEditorAction> _undoStack = new Stack<LevelEditorAction>();
        private Stack<LevelEditorAction> _redoStack = new Stack<LevelEditorAction>();
        
        // UI state
        private bool _showLegend = true;
        private bool _showHelp;
        private bool _stylesInitialized;
        private GUIStyle _gridCellStyle;
        
        // Tooltip content cache
        private static class Tooltips
        {
            public static readonly GUIContent Paint = new GUIContent("Paint", "Click or drag to add elements to the grid (1)");
            public static readonly GUIContent Erase = new GUIContent("Erase", "Click or drag to remove elements (2)");
            public static readonly GUIContent Select = new GUIContent("Select", "Click to select, drag to move. Shift+click for multi-select (3)");
            public static readonly GUIContent Fill = new GUIContent("Fill", "Drag to fill a rectangular area (4)");
            public static readonly GUIContent Save = new GUIContent("Save Level", "Save changes to disk (Ctrl+S)");
            public static readonly GUIContent Undo = new GUIContent("Undo", "Undo last action (Ctrl+Z)");
            public static readonly GUIContent Redo = new GUIContent("Redo", "Redo last undone action (Ctrl+Shift+Z)");
            public static readonly GUIContent Help = new GUIContent("?", "Open help documentation");
            public static readonly GUIContent ShowAll = new GUIContent("Show All", "Make all layers visible");
            public static readonly GUIContent HideAll = new GUIContent("Hide All", "Hide all layers");
        }
        
        #endregion
        
        #region Selection Item
        
        private class SelectedItem
        {
            public EditorLayer Layer;
            public Vector2Int Position;
            public int Index;
            
            public SelectedItem(EditorLayer layer, Vector2Int position, int index = -1)
            {
                Layer = layer;
                Position = position;
                Index = index;
            }
        }
        
        #endregion
        
        #region Window Setup
        
        [MenuItem("Window/Breaking Hue/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(900, 650);
        }
        
        private void OnEnable()
        {
            _stylesInitialized = false;
            Undo.undoRedoPerformed += OnUnityUndoRedo;
        }
        
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUnityUndoRedo;
        }
        
        private void OnDestroy()
        {
            if (_hasUnsavedChanges)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. Save before closing?",
                    "Save", "Don't Save", "Cancel"
                );
                
                if (choice == 0) // Save
                {
                    SaveLevel();
                }
                // choice == 1 (Don't Save) or 2 (Cancel) - just close
            }
        }
        
        private void OnUnityUndoRedo()
        {
            Repaint();
        }
        
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            _gridCellStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8
            };
            
            _stylesInitialized = true;
        }
        
        private void UpdateWindowTitle()
        {
            string title = "Level Editor";
            if (_hasUnsavedChanges)
            {
                title += "*";
            }
            titleContent = new GUIContent(title);
        }
        
        #endregion
        
        #region Main GUI
        
        private void OnGUI()
        {
            InitializeStyles();
            UpdateWindowTitle();
            
            // Top toolbar
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            
            // Left panel - Layers
            DrawLayerPanel();
            
            // Center - Grid
            DrawGridPanel();
            
            // Right panel - Properties
            DrawPropertiesPanel();
            
            EditorGUILayout.EndHorizontal();
            
            // Handle input
            HandleInput();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Undo/Redo buttons
            GUI.enabled = _undoStack.Count > 0;
            if (GUILayout.Button(new GUIContent($"Undo ({_undoStack.Count})", Tooltips.Undo.tooltip), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                PerformUndo();
            }
            GUI.enabled = _redoStack.Count > 0;
            if (GUILayout.Button(new GUIContent($"Redo ({_redoStack.Count})", Tooltips.Redo.tooltip), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                PerformRedo();
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            // Unsaved indicator
            if (_hasUnsavedChanges)
            {
                GUILayout.Label("Unsaved changes", EditorStyles.miniLabel);
            }
            
            GUILayout.FlexibleSpace();
            
            // Help button
            if (GUILayout.Button(Tooltips.Help, EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                LevelEditorHelpWindow.ShowWindow();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion
        
        #region Layer Panel
        
        private void DrawLayerPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LayerPanelWidth));
            
            GUILayout.Label("Layers", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Level asset field
            EditorGUI.BeginChangeCheck();
            var newLevel = (LevelData)EditorGUILayout.ObjectField("Level Data", _currentLevel, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newLevel != _currentLevel)
                {
                    if (_hasUnsavedChanges)
                    {
                        int choice = EditorUtility.DisplayDialogComplex(
                            "Unsaved Changes",
                            "You have unsaved changes. Save before switching?",
                            "Save", "Don't Save", "Cancel"
                        );
                        
                        if (choice == 0) SaveLevel();
                        else if (choice == 2) { Repaint(); return; } // Cancel
                    }
                    
                    _currentLevel = newLevel;
                    LoadLevelIntoWorkingCopy();
                }
            }
            
            GUILayout.Space(10);
            
            if (_currentLevel == null)
            {
                if (GUILayout.Button("Create New Level", GUILayout.Height(30)))
                {
                    CreateNewLevel();
                }
            }
            else
            {
                // Visibility controls
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(Tooltips.ShowAll, GUILayout.Height(20)))
                {
                    for (int i = 0; i < _layerVisibility.Length; i++)
                        _layerVisibility[i] = true;
                }
                if (GUILayout.Button(Tooltips.HideAll, GUILayout.Height(20)))
                {
                    for (int i = 0; i < _layerVisibility.Length; i++)
                        _layerVisibility[i] = false;
                }
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                // Layer buttons with visibility toggles
                DrawLayerButtonWithVisibility(EditorLayer.Ground, "Ground (Floor)", 0);
                DrawLayerButtonWithVisibility(EditorLayer.Walls, "Walls", 1);
                DrawLayerButtonWithVisibility(EditorLayer.Barriers, "Barriers", 2);
                DrawLayerButtonWithVisibility(EditorLayer.Pickups, "Pickups", 3);
                DrawLayerButtonWithVisibility(EditorLayer.Barrels, "Barrels", 4);
                DrawLayerButtonWithVisibility(EditorLayer.Bots, "Bots", 5);
                DrawLayerButtonWithVisibility(EditorLayer.BotPaths, "Bot Paths", 6);
                DrawLayerButtonWithVisibility(EditorLayer.Portals, "Portals", 7);
                DrawLayerButtonWithVisibility(EditorLayer.HiddenAreas, "Hidden Areas", 8);
                
                GUILayout.Space(10);
                
                // Tools
                GUILayout.Label("Tools", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                DrawToolButton(EditorTool.Paint, Tooltips.Paint);
                DrawToolButton(EditorTool.Erase, Tooltips.Erase);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                DrawToolButton(EditorTool.Select, Tooltips.Select);
                DrawToolButton(EditorTool.Fill, Tooltips.Fill);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                
                // Save button
                GUI.backgroundColor = _hasUnsavedChanges ? new Color(1f, 0.8f, 0.5f) : Color.white;
                if (GUILayout.Button(Tooltips.Save, GUILayout.Height(25)))
                {
                    SaveLevel();
                }
                GUI.backgroundColor = Color.white;
                
                // Revert button
                if (_hasUnsavedChanges)
                {
                    if (GUILayout.Button("Revert Changes", GUILayout.Height(20)))
                    {
                        if (EditorUtility.DisplayDialog("Revert Changes", 
                            "Are you sure you want to revert all unsaved changes?", "Revert", "Cancel"))
                        {
                            LoadLevelIntoWorkingCopy();
                        }
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLayerButtonWithVisibility(EditorLayer layer, string label, int visibilityIndex)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Eye toggle for visibility
            bool wasVisible = _layerVisibility[visibilityIndex];
            string eyeIcon = wasVisible ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover";
            GUIContent eyeContent = new GUIContent(EditorGUIUtility.IconContent(eyeIcon))
            {
                tooltip = wasVisible ? "Hide layer" : "Show layer"
            };
            
            if (GUILayout.Button(eyeContent, GUILayout.Width(25), GUILayout.Height(25)))
            {
                _layerVisibility[visibilityIndex] = !_layerVisibility[visibilityIndex];
            }
            
            // Layer selection button
            bool isSelected = _currentLayer == layer;
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
            
            if (GUILayout.Button(label, GUILayout.Height(25)))
            {
                _currentLayer = layer;
                ClearSelection();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawToolButton(EditorTool tool, GUIContent content)
        {
            bool isSelected = _currentTool == tool;
            GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
            
            if (GUILayout.Button(content, GUILayout.Height(20)))
            {
                _currentTool = tool;
                if (tool != EditorTool.Select)
                {
                    ClearSelection();
                }
            }
            
            GUI.backgroundColor = Color.white;
        }
        
        #endregion
        
        #region Grid Panel
        
        private void DrawGridPanel()
        {
            Rect gridRect = GUILayoutUtility.GetRect(
                position.width - LayerPanelWidth - PropertiesPanelWidth - 20,
                position.height - ToolbarHeight - 20,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );
            
            // Background
            EditorGUI.DrawRect(gridRect, new Color(0.15f, 0.15f, 0.15f));
            
            if (_workingCopy == null)
            {
                GUI.Label(gridRect, "No level loaded.\nSelect or create a level.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            // Calculate grid dimensions
            float cellDisplaySize = CellSize * _zoom;
            float gridWidth = GridSize * cellDisplaySize;
            float gridHeight = GridSize * cellDisplaySize;
            
            // Grid offset for centering/panning
            Vector2 gridStart = new Vector2(
                gridRect.x + (gridRect.width - gridWidth) / 2 + _gridOffset.x,
                gridRect.y + (gridRect.height - gridHeight) / 2 + _gridOffset.y
            );
            
            // Draw grid
            GUI.BeginClip(gridRect);
            Vector2 clipOffset = new Vector2(-gridRect.x, -gridRect.y);
            
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    Rect cellRect = new Rect(
                        gridStart.x + x * cellDisplaySize + clipOffset.x,
                        gridStart.y + (GridSize - 1 - y) * cellDisplaySize + clipOffset.y,
                        cellDisplaySize - 1,
                        cellDisplaySize - 1
                    );
                    
                    // Determine cell color based on contents
                    Color cellColor = GetCellColor(x, y);
                    EditorGUI.DrawRect(cellRect, cellColor);
                    
                    // Draw selection highlight
                    Vector2Int pos = new Vector2Int(x, y);
                    if (_selection.Any(s => s.Position == pos))
                    {
                        DrawSelectionHighlight(cellRect);
                    }
                    
                    // Draw fill preview
                    if (_isFilling && IsInFillRect(x, y))
                    {
                        Color previewColor = GetLayerBaseColor(_currentLayer);
                        previewColor.a = 0.5f;
                        EditorGUI.DrawRect(cellRect, previewColor);
                        DrawSelectionHighlight(cellRect, new Color(0f, 1f, 0f, 0.8f));
                    }
                    
                    // Draw grid lines
                    if (_zoom > 0.5f)
                    {
                        Handles.color = new Color(0.3f, 0.3f, 0.3f);
                        Handles.DrawLine(
                            new Vector3(cellRect.x, cellRect.y),
                            new Vector3(cellRect.x + cellRect.width, cellRect.y)
                        );
                        Handles.DrawLine(
                            new Vector3(cellRect.x, cellRect.y),
                            new Vector3(cellRect.x, cellRect.y + cellRect.height)
                        );
                    }
                }
            }
            
            // Draw bot paths
            if (_layerVisibility[5] || _layerVisibility[6]) // Bots or BotPaths visible
            {
                DrawBotPaths(gridStart + clipOffset, cellDisplaySize);
            }
            
            // Draw drag preview
            if (_isDraggingSelection && _selection.Count > 0)
            {
                DrawDragPreview(gridStart + clipOffset, cellDisplaySize);
            }
            
            GUI.EndClip();
            
            // Info overlay
            DrawGridOverlay(gridRect);
        }
        
        private void DrawSelectionHighlight(Rect cellRect, Color? color = null)
        {
            Color highlightColor = color ?? new Color(1f, 1f, 0f, 0.8f);
            float thickness = 2f;
            
            // Top
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, thickness), highlightColor);
            // Bottom
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y + cellRect.height - thickness, cellRect.width, thickness), highlightColor);
            // Left
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, thickness, cellRect.height), highlightColor);
            // Right
            EditorGUI.DrawRect(new Rect(cellRect.x + cellRect.width - thickness, cellRect.y, thickness, cellRect.height), highlightColor);
        }
        
        private void DrawDragPreview(Vector2 gridStart, float cellSize)
        {
            Vector2Int offset = _dragCurrentPos - _dragStartPos;
            
            foreach (var item in _selection)
            {
                Vector2Int newPos = item.Position + offset;
                if (newPos.x >= 0 && newPos.x < GridSize && newPos.y >= 0 && newPos.y < GridSize)
                {
                    Rect cellRect = new Rect(
                        gridStart.x + newPos.x * cellSize,
                        gridStart.y + (GridSize - 1 - newPos.y) * cellSize,
                        cellSize - 1,
                        cellSize - 1
                    );
                    
                    Color previewColor = GetLayerBaseColor(item.Layer);
                    previewColor.a = 0.5f;
                    EditorGUI.DrawRect(cellRect, previewColor);
                }
            }
        }
        
        private bool IsInFillRect(int x, int y)
        {
            int minX = Mathf.Min(_fillStartPos.x, _fillEndPos.x);
            int maxX = Mathf.Max(_fillStartPos.x, _fillEndPos.x);
            int minY = Mathf.Min(_fillStartPos.y, _fillEndPos.y);
            int maxY = Mathf.Max(_fillStartPos.y, _fillEndPos.y);
            
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }
        
        private void DrawGridOverlay(Rect gridRect)
        {
            // Zoom indicator
            GUI.Label(new Rect(gridRect.x + 5, gridRect.y + 5, 100, 20), $"Zoom: {_zoom:F1}x", EditorStyles.miniLabel);
            
            // Selection count
            if (_selection.Count > 0)
            {
                GUI.Label(new Rect(gridRect.x + 5, gridRect.y + 20, 150, 20), 
                    $"Selected: {_selection.Count} items", EditorStyles.miniLabel);
            }
            
            // Fill preview size
            if (_isFilling)
            {
                int width = Mathf.Abs(_fillEndPos.x - _fillStartPos.x) + 1;
                int height = Mathf.Abs(_fillEndPos.y - _fillStartPos.y) + 1;
                GUI.Label(new Rect(gridRect.x + 5, gridRect.y + 20, 150, 20), 
                    $"Fill: {width} x {height}", EditorStyles.miniLabel);
            }
        }
        
        private Color GetLayerBaseColor(EditorLayer layer)
        {
            switch (layer)
            {
                case EditorLayer.Ground: return new Color(0.3f, 0.3f, 0.35f);
                case EditorLayer.Walls: return Color.white;
                case EditorLayer.Barriers: return _selectedColor.ToColor();
                case EditorLayer.Pickups: return Color.Lerp(_selectedColor.ToColor(), Color.white, 0.3f);
                case EditorLayer.Barrels: return Color.Lerp(_selectedColor.ToColor(), Color.black, 0.3f);
                case EditorLayer.Bots: return Color.cyan;
                case EditorLayer.Portals: return _isCheckpoint ? new Color(1f, 0.8f, 0f) : new Color(0f, 0.8f, 1f);
                case EditorLayer.HiddenAreas: return new Color(0.25f, 0.25f, 0.25f);
                default: return Color.gray;
            }
        }
        
        private Color GetCellColor(int x, int y)
        {
            Vector2Int pos = new Vector2Int(x, y);
            Color baseColor = new Color(0.2f, 0.2f, 0.2f);
            
            // Check each layer based on visibility
            if (_layerVisibility[0] && _workingCopy.FloorTiles.Contains(pos))
            {
                baseColor = new Color(0.3f, 0.3f, 0.35f);
            }
            
            if (_layerVisibility[1] && _workingCopy.WallTiles.Contains(pos))
            {
                return Color.white;
            }
            
            if (_layerVisibility[2])
            {
                var barrier = _workingCopy.Barriers.Find(b => b.Position == pos);
                if (barrier != null)
                {
                    Color c = barrier.Color.ToColor();
                    c.a = 0.7f;
                    return Color.Lerp(baseColor, c, 0.8f);
                }
            }
            
            if (_layerVisibility[3])
            {
                var pickup = _workingCopy.Pickups.Find(p => p.Position == pos);
                if (pickup != null)
                {
                    Color c = pickup.Color.ToColor();
                    return Color.Lerp(c, Color.white, 0.3f);
                }
            }
            
            if (_layerVisibility[4])
            {
                var barrel = _workingCopy.Barrels.Find(b => b.Position == pos);
                if (barrel != null)
                {
                    Color c = barrel.Color.ToColor();
                    return Color.Lerp(c, Color.black, 0.3f);
                }
            }
            
            if (_layerVisibility[5])
            {
                var bot = _workingCopy.Bots.Find(b => b.StartPosition == pos);
                if (bot != null)
                {
                    // Highlight bots without paths
                    if (_currentLayer == EditorLayer.BotPaths)
                    {
                        bool hasPath = bot.InlineWaypoints != null && bot.InlineWaypoints.Count > 1;
                        if (!hasPath && bot.PathData == null)
                        {
                            return new Color(1f, 0.3f, 0.3f); // Red warning
                        }
                    }
                    return Color.cyan;
                }
            }
            
            if (_layerVisibility[7])
            {
                var portal = _workingCopy.Portals.Find(p => p.Position == pos);
                if (portal != null)
                {
                    return portal.IsCheckpoint ? new Color(1f, 0.8f, 0f) : new Color(0f, 0.8f, 1f);
                }
                
                if (_workingCopy.PlayerSpawnPosition == pos)
                {
                    return Color.green;
                }
            }
            
            if (_layerVisibility[8])
            {
                var hidden = _workingCopy.HiddenBlocks.Find(h => h.Position == pos);
                if (hidden != null)
                {
                    return new Color(0.25f, 0.25f, 0.25f);
                }
            }
            
            return baseColor;
        }
        
        private void DrawBotPaths(Vector2 gridStart, float cellSize)
        {
            if (_workingCopy.Bots == null) return;
            
            foreach (var bot in _workingCopy.Bots)
            {
                var waypoints = bot.InlineWaypoints ?? bot.PathData?.waypoints;
                if (waypoints == null || waypoints.Count < 1) continue;
                
                bool isSelectedBot = _selectedBotForPath != null && _selectedBotForPath.BotId == bot.BotId;
                Handles.color = isSelectedBot ? Color.green : Color.cyan;
                
                // Draw waypoint numbers
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Vector2 pos = GridToEditor(waypoints[i], gridStart, cellSize);
                    
                    if (_zoom > 0.7f)
                    {
                        Rect labelRect = new Rect(pos.x - 8, pos.y - 8, 16, 16);
                        GUI.Label(labelRect, (i + 1).ToString(), EditorStyles.whiteMiniLabel);
                    }
                }
                
                // Draw path lines with arrows
                if (waypoints.Count >= 2)
                {
                    for (int i = 0; i < waypoints.Count - 1; i++)
                    {
                        Vector2 from = GridToEditor(waypoints[i], gridStart, cellSize);
                        Vector2 to = GridToEditor(waypoints[i + 1], gridStart, cellSize);
                        Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
                        
                        // Draw arrow
                        if (_zoom > 0.5f)
                        {
                            DrawArrow(from, to);
                        }
                    }
                    
                    // Draw loop connection if applicable
                    if (bot.PathMode == PathMode.Loop && waypoints.Count > 2)
                    {
                        Vector2 from = GridToEditor(waypoints[waypoints.Count - 1], gridStart, cellSize);
                        Vector2 to = GridToEditor(waypoints[0], gridStart, cellSize);
                        Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.5f);
                        Handles.DrawDottedLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y), 4f);
                        Handles.color = isSelectedBot ? Color.green : Color.cyan;
                    }
                    
                    // Draw pingpong indicators
                    if (bot.PathMode == PathMode.PingPong)
                    {
                        Vector2 first = GridToEditor(waypoints[0], gridStart, cellSize);
                        Vector2 last = GridToEditor(waypoints[waypoints.Count - 1], gridStart, cellSize);
                        
                        Handles.color = Color.yellow;
                        Handles.DrawSolidDisc(new Vector3(first.x, first.y, 0), Vector3.forward, 4f);
                        Handles.DrawSolidDisc(new Vector3(last.x, last.y, 0), Vector3.forward, 4f);
                        Handles.color = isSelectedBot ? Color.green : Color.cyan;
                    }
                }
            }
            
            // Draw current path being edited
            if (_currentBotPath.Count > 0)
            {
                Handles.color = Color.yellow;
                
                // Draw waypoint numbers for current path
                for (int i = 0; i < _currentBotPath.Count; i++)
                {
                    Vector2 pos = GridToEditor(_currentBotPath[i], gridStart, cellSize);
                    Handles.DrawSolidDisc(new Vector3(pos.x, pos.y, 0), Vector3.forward, 5f);
                    
                    if (_zoom > 0.7f)
                    {
                        Handles.color = Color.black;
                        Rect labelRect = new Rect(pos.x - 4, pos.y - 6, 16, 16);
                        GUI.Label(labelRect, (i + 1).ToString(), EditorStyles.whiteMiniLabel);
                        Handles.color = Color.yellow;
                    }
                }
                
                // Draw lines
                if (_currentBotPath.Count > 1)
                {
                    for (int i = 0; i < _currentBotPath.Count - 1; i++)
                    {
                        Vector2 from = GridToEditor(_currentBotPath[i], gridStart, cellSize);
                        Vector2 to = GridToEditor(_currentBotPath[i + 1], gridStart, cellSize);
                        Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
                    }
                }
            }
        }
        
        private void DrawArrow(Vector2 from, Vector2 to)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 mid = Vector2.Lerp(from, to, 0.6f);
            
            float arrowSize = 6f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            
            Vector3 tip = new Vector3(mid.x, mid.y, 0);
            Vector3 left = new Vector3(mid.x - direction.x * arrowSize + perpendicular.x * arrowSize * 0.5f,
                                       mid.y - direction.y * arrowSize + perpendicular.y * arrowSize * 0.5f, 0);
            Vector3 right = new Vector3(mid.x - direction.x * arrowSize - perpendicular.x * arrowSize * 0.5f,
                                        mid.y - direction.y * arrowSize - perpendicular.y * arrowSize * 0.5f, 0);
            
            Handles.DrawLine(tip, left);
            Handles.DrawLine(tip, right);
        }
        
        private Vector2 GridToEditor(Vector2Int gridPos, Vector2 gridStart, float cellSize)
        {
            return new Vector2(
                gridStart.x + gridPos.x * cellSize + cellSize / 2,
                gridStart.y + (GridSize - 1 - gridPos.y) * cellSize + cellSize / 2
            );
        }
        
        #endregion
        
        #region Properties Panel
        
        private void DrawPropertiesPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(PropertiesPanelWidth));
            _propertiesScrollPosition = EditorGUILayout.BeginScrollView(_propertiesScrollPosition);
            
            GUILayout.Label("Properties", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            if (_workingCopy != null)
            {
                // Level properties
                EditorGUI.BeginChangeCheck();
                _workingCopy.LevelName = EditorGUILayout.TextField("Level Name", _workingCopy.LevelName);
                _workingCopy.LevelIndex = EditorGUILayout.IntField("Level Index", _workingCopy.LevelIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    MarkDirty();
                }
                
                GUILayout.Space(10);
                
                // Selection properties
                if (_selection.Count > 0)
                {
                    DrawSelectionProperties();
                }
                else
                {
                    // Layer-specific properties
                    switch (_currentLayer)
                    {
                        case EditorLayer.Barriers:
                        case EditorLayer.Pickups:
                        case EditorLayer.Barrels:
                            DrawColorPicker();
                            break;
                            
                        case EditorLayer.Bots:
                            DrawBotProperties();
                            break;
                            
                        case EditorLayer.BotPaths:
                            DrawBotPathProperties();
                            break;
                            
                        case EditorLayer.Portals:
                            DrawPortalProperties();
                            break;
                    }
                }
                
                GUILayout.Space(10);
                
                // Statistics
                GUILayout.Label("Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Floor tiles:", _workingCopy.FloorTiles.Count.ToString());
                EditorGUILayout.LabelField("Walls:", _workingCopy.WallTiles.Count.ToString());
                EditorGUILayout.LabelField("Barriers:", _workingCopy.Barriers.Count.ToString());
                EditorGUILayout.LabelField("Pickups:", _workingCopy.Pickups.Count.ToString());
                EditorGUILayout.LabelField("Barrels:", _workingCopy.Barrels.Count.ToString());
                EditorGUILayout.LabelField("Bots:", _workingCopy.Bots.Count.ToString());
                EditorGUILayout.LabelField("Portals:", _workingCopy.Portals.Count.ToString());
                
                GUILayout.Space(10);
                
                // Legend
                DrawLegend();
                
                GUILayout.Space(10);
                
                // Context help
                DrawContextHelp();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSelectionProperties()
        {
            GUILayout.Label($"Selection ({_selection.Count} items)", EditorStyles.boldLabel);
            
            // Get first selected item for reference
            var firstItem = _selection[0];
            
            // Check if all items are from the same layer
            bool sameLayer = _selection.All(s => s.Layer == firstItem.Layer);
            
            if (sameLayer)
            {
                EditorGUILayout.LabelField("Layer:", firstItem.Layer.ToString());
                
                // Color editing for color-based elements
                if (firstItem.Layer == EditorLayer.Barriers || 
                    firstItem.Layer == EditorLayer.Pickups || 
                    firstItem.Layer == EditorLayer.Barrels)
                {
                    GUILayout.Label("Change Color", EditorStyles.boldLabel);
                    DrawColorPicker();
                    
                    if (GUILayout.Button("Apply Color to Selection"))
                    {
                        ApplyColorToSelection(_selectedColor);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Layer:", "Mixed");
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Delete Selection (Del)"))
            {
                DeleteSelection();
            }
            
            if (GUILayout.Button("Clear Selection (Esc)"))
            {
                ClearSelection();
            }
        }
        
        private void DrawLegend()
        {
            _showLegend = EditorGUILayout.Foldout(_showLegend, "Legend", true);
            
            if (_showLegend)
            {
                EditorGUI.indentLevel++;
                
                DrawLegendItem("Empty", new Color(0.2f, 0.2f, 0.2f));
                DrawLegendItem("Floor", new Color(0.3f, 0.3f, 0.35f));
                DrawLegendItem("Wall", Color.white);
                DrawLegendItem("Bot", Color.cyan);
                DrawLegendItem("Bot (no path)", new Color(1f, 0.3f, 0.3f));
                DrawLegendItem("Portal", new Color(0f, 0.8f, 1f));
                DrawLegendItem("Checkpoint", new Color(1f, 0.8f, 0f));
                DrawLegendItem("Player Spawn", Color.green);
                DrawLegendItem("Hidden Area", new Color(0.25f, 0.25f, 0.25f));
                
                GUILayout.Space(5);
                GUILayout.Label("Colors:", EditorStyles.miniLabel);
                
                foreach (ColorType color in Enum.GetValues(typeof(ColorType)))
                {
                    DrawLegendItem(color.GetDisplayName(), color.ToColor());
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawLegendItem(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            
            Rect swatchRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
            EditorGUI.DrawRect(swatchRect, color);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, swatchRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y + swatchRect.height - 1, swatchRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, 1, swatchRect.height), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x + swatchRect.width - 1, swatchRect.y, 1, swatchRect.height), Color.black);
            
            GUILayout.Space(5);
            GUILayout.Label(label, EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawContextHelp()
        {
            _showHelp = EditorGUILayout.Foldout(_showHelp, "Quick Help", true);
            
            if (_showHelp)
            {
                string helpText = GetContextHelp();
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }
        }
        
        private string GetContextHelp()
        {
            switch (_currentLayer)
            {
                case EditorLayer.Ground:
                    return "Click to place floor tiles. Drag to paint multiple tiles. Erase tool removes tiles.";
                    
                case EditorLayer.Walls:
                    return "Click to place walls. Walls block player and bot movement.";
                    
                case EditorLayer.Barriers:
                    return "Select a color, then click to place barriers. Players need matching masks to pass through.";
                    
                case EditorLayer.Pickups:
                    return "Select a color, then click to place pickups. Players collect these to gain masks.";
                    
                case EditorLayer.Barrels:
                    return "Select a color, then click to place barrels. Barrels explode when triggered.";
                    
                case EditorLayer.Bots:
                    return "Click to place bots. Set color and path mode in properties. Use Bot Paths layer to define patrol routes.";
                    
                case EditorLayer.BotPaths:
                    return "1. Click on a bot to select it\n2. Click grid cells to add waypoints\n3. Click 'Apply Path' when done";
                    
                case EditorLayer.Portals:
                    return "Click to place portals. Toggle 'Is Checkpoint' before placing. Select two portals and click 'Create Link' to connect them.";
                    
                case EditorLayer.HiddenAreas:
                    return "Click to mark hidden areas. These areas are concealed until the player discovers them.";
                    
                default:
                    return "Select a layer to get started.";
            }
        }
        
        private void DrawColorPicker()
        {
            GUILayout.Label("Color Selection", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            DrawColorButton(ColorType.Red, "R");
            DrawColorButton(ColorType.Yellow, "Y");
            DrawColorButton(ColorType.Blue, "B");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawColorButton(ColorType.Orange, "O");
            DrawColorButton(ColorType.Green, "G");
            DrawColorButton(ColorType.Purple, "P");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawColorButton(ColorType.Black, "K");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("Selected:", _selectedColor.GetDisplayName());
        }
        
        private void DrawColorButton(ColorType color, string label)
        {
            bool isSelected = _selectedColor == color;
            Color guiColor = color.ToColor();
            guiColor.a = 1f;
            GUI.backgroundColor = guiColor;
            
            GUIStyle style = new GUIStyle(GUI.skin.button);
            if (isSelected)
            {
                style.fontStyle = FontStyle.Bold;
            }
            
            if (GUILayout.Button(label, style, GUILayout.Width(30), GUILayout.Height(25)))
            {
                _selectedColor = color;
            }
            
            GUI.backgroundColor = Color.white;
        }
        
        private void DrawBotProperties()
        {
            GUILayout.Label("Bot Properties", EditorStyles.boldLabel);
            
            DrawColorPicker();
            
            GUILayout.Space(5);
            
            _currentBotId = EditorGUILayout.TextField("Bot ID", _currentBotId);
            _currentPathMode = (PathMode)EditorGUILayout.EnumPopup("Path Mode", _currentPathMode);
            
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Place bots here, then switch to Bot Paths layer to define patrol routes.", MessageType.Info);
        }
        
        private void DrawBotPathProperties()
        {
            GUILayout.Label("Bot Path Editor", EditorStyles.boldLabel);
            
            // Bot selection dropdown
            if (_workingCopy.Bots.Count > 0)
            {
                GUILayout.Label("Select Bot", EditorStyles.miniBoldLabel);
                
                string[] botNames = _workingCopy.Bots.Select(b => 
                    $"{b.BotId?.Substring(0, Mathf.Min(8, b.BotId?.Length ?? 0)) ?? "Unknown"} @ ({b.StartPosition.x}, {b.StartPosition.y})"
                ).ToArray();
                
                int selectedIndex = _selectedBotForPath != null 
                    ? _workingCopy.Bots.FindIndex(b => b.BotId == _selectedBotForPath.BotId) 
                    : -1;
                
                int newIndex = EditorGUILayout.Popup("Bot", selectedIndex, botNames);
                if (newIndex != selectedIndex && newIndex >= 0)
                {
                    _selectedBotForPath = _workingCopy.Bots[newIndex];
                    _currentBotPath.Clear();
                    
                    // Load existing path
                    if (_selectedBotForPath.InlineWaypoints != null)
                    {
                        _currentBotPath = new List<Vector2Int>(_selectedBotForPath.InlineWaypoints);
                    }
                }
                
                // Show selected bot info
                if (_selectedBotForPath != null)
                {
                    EditorGUILayout.LabelField("Selected:", _selectedBotForPath.BotId);
                    _currentPathMode = (PathMode)EditorGUILayout.EnumPopup("Path Mode", _currentPathMode);
                    
                    bool hasPath = _selectedBotForPath.InlineWaypoints != null && _selectedBotForPath.InlineWaypoints.Count > 1;
                    if (!hasPath && _selectedBotForPath.PathData == null)
                    {
                        EditorGUILayout.HelpBox("This bot has no path! Add waypoints below.", MessageType.Warning);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No bots in level. Add bots on the Bots layer first.", MessageType.Warning);
            }
            
            GUILayout.Space(10);
            
            GUILayout.Label("Current Path", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Waypoints:", _currentBotPath.Count.ToString());
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Path"))
            {
                _currentBotPath.Clear();
            }
            
            if (_currentBotPath.Count > 0 && GUILayout.Button("Remove Last"))
            {
                _currentBotPath.RemoveAt(_currentBotPath.Count - 1);
            }
            EditorGUILayout.EndHorizontal();
            
            if (_currentBotPath.Count >= 2 && _selectedBotForPath != null)
            {
                if (GUILayout.Button("Apply Path to Selected Bot", GUILayout.Height(25)))
                {
                    ApplyPathToSelectedBot();
                }
            }
            
            GUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "1. Select a bot from the dropdown\n" +
                "2. Click on the grid to add waypoints\n" +
                "3. First waypoint should be at bot's position\n" +
                "4. Click 'Apply Path' when done",
                MessageType.Info);
        }
        
        // Cross-level linking state
        private LevelData _destinationLevel;
        private int _destinationPortalIndex = -1;
        
        private void DrawPortalProperties()
        {
            GUILayout.Label("Portal Properties", EditorStyles.boldLabel);
            
            _isCheckpoint = EditorGUILayout.Toggle("Is Checkpoint", _isCheckpoint);
            
            GUILayout.Space(10);
            
            // Same-level linking section
            GUILayout.Label("Same-Level Portal Linking", EditorStyles.boldLabel);
            
            if (_selectedPortalA != null)
            {
                EditorGUILayout.LabelField("Portal A:", $"({_selectedPortalA.Position.x}, {_selectedPortalA.Position.y})");
            }
            else
            {
                EditorGUILayout.LabelField("Portal A:", "Not selected");
            }
            
            if (_selectedPortalB != null)
            {
                EditorGUILayout.LabelField("Portal B:", $"({_selectedPortalB.Position.x}, {_selectedPortalB.Position.y})");
            }
            else
            {
                EditorGUILayout.LabelField("Portal B:", "Not selected");
            }
            
            if (_selectedPortalA != null && _selectedPortalB != null)
            {
                if (GUILayout.Button("Create Same-Level Link"))
                {
                    CreatePortalLink();
                }
            }
            
            if (GUILayout.Button("Clear Selection"))
            {
                _selectedPortalA = null;
                _selectedPortalB = null;
            }
            
            GUILayout.Space(10);
            
            // Cross-level linking section
            GUILayout.Label("Cross-Level Portal Linking", EditorStyles.boldLabel);
            
            if (_selectedPortalA != null)
            {
                EditorGUILayout.LabelField("Source Portal:", $"({_selectedPortalA.Position.x}, {_selectedPortalA.Position.y}) in {_currentLevel?.levelName}");
                
                // Destination level selection
                EditorGUI.BeginChangeCheck();
                _destinationLevel = (LevelData)EditorGUILayout.ObjectField("Destination Level", _destinationLevel, typeof(LevelData), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _destinationPortalIndex = -1; // Reset portal selection when level changes
                }
                
                // Destination portal selection
                if (_destinationLevel != null && _destinationLevel.portalLayer?.portals != null && _destinationLevel.portalLayer.portals.Count > 0)
                {
                    string[] portalNames = _destinationLevel.portalLayer.portals
                        .Select((p, i) => $"Portal {i + 1} @ ({p.position.x}, {p.position.y}){(p.isCheckpoint ? " [Checkpoint]" : "")}")
                        .ToArray();
                    
                    _destinationPortalIndex = EditorGUILayout.Popup("Destination Portal", _destinationPortalIndex, portalNames);
                    
                    if (_destinationPortalIndex >= 0 && GUILayout.Button("Create Cross-Level Link", GUILayout.Height(25)))
                    {
                        CreateCrossLevelPortalLink();
                    }
                }
                else if (_destinationLevel != null)
                {
                    EditorGUILayout.HelpBox("Destination level has no portals.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a portal in the grid first (click on it).", MessageType.Info);
            }
            
            GUILayout.Space(10);
            
            // Existing link display
            if (_selectedPortalA != null && _selectedPortalA.Link != null)
            {
                GUILayout.Label("Current Link", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Link Asset", _selectedPortalA.Link, typeof(EntranceExitLink), false);
                
                if (GUILayout.Button("Remove Link"))
                {
                    _selectedPortalA.Link = null;
                    MarkDirty();
                }
            }
            
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Click on a portal to select it, then choose a destination level and portal to create a cross-level link.", MessageType.Info);
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleInput()
        {
            Event e = Event.current;
            
            if (e == null) return;
            
            // Keyboard shortcuts (work even without level loaded)
            if (e.type == EventType.KeyDown)
            {
                HandleKeyboardShortcuts(e);
            }
            
            if (_workingCopy == null) return;
            
            // Zoom with scroll wheel
            if (e.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.1f, 0.25f, 3f);
                e.Use();
                Repaint();
            }
            
            // Pan with middle mouse button
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _gridOffset += e.delta;
                e.Use();
                Repaint();
            }
            
            // Tool operations with left click
            if (e.button == 0)
            {
                Vector2Int? gridPos = ScreenToGrid(e.mousePosition);
                
                switch (_currentTool)
                {
                    case EditorTool.Paint:
                    case EditorTool.Erase:
                        HandlePaintEraseInput(e, gridPos);
                        break;
                        
                    case EditorTool.Select:
                        HandleSelectInput(e, gridPos);
                        break;
                        
                    case EditorTool.Fill:
                        HandleFillInput(e, gridPos);
                        break;
                }
            }
            
            // Right click for context actions
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                Vector2Int? gridPos = ScreenToGrid(e.mousePosition);
                if (gridPos.HasValue)
                {
                    HandleRightClick(gridPos.Value);
                    e.Use();
                }
            }
        }
        
        private void HandleKeyboardShortcuts(Event e)
        {
            bool ctrl = e.control || e.command;
            
            // Undo: Ctrl+Z
            if (ctrl && e.keyCode == KeyCode.Z && !e.shift)
            {
                PerformUndo();
                e.Use();
                return;
            }
            
            // Redo: Ctrl+Shift+Z or Ctrl+Y
            if ((ctrl && e.shift && e.keyCode == KeyCode.Z) || (ctrl && e.keyCode == KeyCode.Y))
            {
                PerformRedo();
                e.Use();
                return;
            }
            
            // Save: Ctrl+S
            if (ctrl && e.keyCode == KeyCode.S)
            {
                SaveLevel();
                e.Use();
                return;
            }
            
            // Tool shortcuts: 1-4
            if (e.keyCode == KeyCode.Alpha1 || e.keyCode == KeyCode.Keypad1)
            {
                _currentTool = EditorTool.Paint;
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Alpha2 || e.keyCode == KeyCode.Keypad2)
            {
                _currentTool = EditorTool.Erase;
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Alpha3 || e.keyCode == KeyCode.Keypad3)
            {
                _currentTool = EditorTool.Select;
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Alpha4 || e.keyCode == KeyCode.Keypad4)
            {
                _currentTool = EditorTool.Fill;
                e.Use();
                return;
            }
            
            // Escape: Clear selection or cancel operation
            if (e.keyCode == KeyCode.Escape)
            {
                if (_isFilling)
                {
                    _isFilling = false;
                }
                else if (_isDraggingSelection)
                {
                    _isDraggingSelection = false;
                }
                else
                {
                    ClearSelection();
                }
                e.Use();
                Repaint();
                return;
            }
            
            // Delete: Delete selection
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (_selection.Count > 0)
                {
                    DeleteSelection();
                    e.Use();
                }
                return;
            }
        }
        
        private void HandlePaintEraseInput(Event e, Vector2Int? gridPos)
        {
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && gridPos.HasValue)
            {
                HandleGridClick(gridPos.Value);
                e.Use();
                Repaint();
            }
        }
        
        private void HandleSelectInput(Event e, Vector2Int? gridPos)
        {
            if (e.type == EventType.MouseDown && gridPos.HasValue)
            {
                // Check if clicking on already selected item
                var clickedSelected = _selection.FirstOrDefault(s => s.Position == gridPos.Value);
                
                if (clickedSelected != null)
                {
                    // Start dragging
                    _isDraggingSelection = true;
                    _dragStartPos = gridPos.Value;
                    _dragCurrentPos = gridPos.Value;
                }
                else
                {
                    // Try to select item at position
                    var item = GetItemAtPosition(gridPos.Value);
                    
                    if (item != null)
                    {
                        if (e.shift)
                        {
                            // Add to selection
                            if (!_selection.Any(s => s.Position == item.Position))
                            {
                                _selection.Add(item);
                            }
                        }
                        else
                        {
                            // Replace selection
                            _selection.Clear();
                            _selection.Add(item);
                        }
                    }
                    else if (!e.shift)
                    {
                        // Clicked on empty space without shift - clear selection
                        ClearSelection();
                    }
                }
                
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingSelection && gridPos.HasValue)
            {
                _dragCurrentPos = gridPos.Value;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && _isDraggingSelection)
            {
                if (gridPos.HasValue && _dragCurrentPos != _dragStartPos)
                {
                    MoveSelection(_dragCurrentPos - _dragStartPos);
                }
                _isDraggingSelection = false;
                e.Use();
                Repaint();
            }
        }
        
        private void HandleFillInput(Event e, Vector2Int? gridPos)
        {
            if (e.type == EventType.MouseDown && gridPos.HasValue)
            {
                _isFilling = true;
                _fillStartPos = gridPos.Value;
                _fillEndPos = gridPos.Value;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && _isFilling && gridPos.HasValue)
            {
                _fillEndPos = gridPos.Value;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && _isFilling)
            {
                if (gridPos.HasValue)
                {
                    _fillEndPos = gridPos.Value;
                }
                PerformFill();
                _isFilling = false;
                e.Use();
                Repaint();
            }
        }
        
        private void HandleRightClick(Vector2Int pos)
        {
            GenericMenu menu = new GenericMenu();
            
            // Context menu based on what's at position
            var item = GetItemAtPosition(pos);
            
            if (item != null)
            {
                menu.AddItem(new GUIContent("Delete"), false, () => {
                    DeleteItemAtPosition(pos, item.Layer);
                });
                
                if (item.Layer == EditorLayer.Portals)
                {
                    menu.AddItem(new GUIContent("Set as Player Spawn"), false, () => {
                        SetPlayerSpawn(pos);
                    });
                }
            }
            
            if (_currentLayer == EditorLayer.BotPaths && _currentBotPath.Count > 0)
            {
                menu.AddItem(new GUIContent("Remove Last Waypoint"), false, () => {
                    if (_currentBotPath.Count > 0)
                        _currentBotPath.RemoveAt(_currentBotPath.Count - 1);
                    Repaint();
                });
                
                menu.AddItem(new GUIContent("Clear Path"), false, () => {
                    _currentBotPath.Clear();
                    Repaint();
                });
            }
            
            if (menu.GetItemCount() > 0)
            {
                menu.ShowAsContext();
            }
        }
        
        private SelectedItem GetItemAtPosition(Vector2Int pos)
        {
            // Check layers in reverse order (top to bottom)
            
            if (_layerVisibility[8])
            {
                int idx = _workingCopy.HiddenBlocks.FindIndex(h => h.Position == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.HiddenAreas, pos, idx);
            }
            
            if (_layerVisibility[7])
            {
                int idx = _workingCopy.Portals.FindIndex(p => p.Position == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.Portals, pos, idx);
            }
            
            if (_layerVisibility[5])
            {
                int idx = _workingCopy.Bots.FindIndex(b => b.StartPosition == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.Bots, pos, idx);
            }
            
            if (_layerVisibility[4])
            {
                int idx = _workingCopy.Barrels.FindIndex(b => b.Position == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.Barrels, pos, idx);
            }
            
            if (_layerVisibility[3])
            {
                int idx = _workingCopy.Pickups.FindIndex(p => p.Position == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.Pickups, pos, idx);
            }
            
            if (_layerVisibility[2])
            {
                int idx = _workingCopy.Barriers.FindIndex(b => b.Position == pos);
                if (idx >= 0) return new SelectedItem(EditorLayer.Barriers, pos, idx);
            }
            
            if (_layerVisibility[1] && _workingCopy.WallTiles.Contains(pos))
            {
                return new SelectedItem(EditorLayer.Walls, pos);
            }
            
            if (_layerVisibility[0] && _workingCopy.FloorTiles.Contains(pos))
            {
                return new SelectedItem(EditorLayer.Ground, pos);
            }
            
            return null;
        }
        
        private Vector2Int? ScreenToGrid(Vector2 screenPos)
        {
            Rect gridRect = new Rect(
                LayerPanelWidth,
                ToolbarHeight,
                position.width - LayerPanelWidth - PropertiesPanelWidth,
                position.height - ToolbarHeight
            );
            
            if (!gridRect.Contains(screenPos)) return null;
            
            float cellDisplaySize = CellSize * _zoom;
            float gridWidth = GridSize * cellDisplaySize;
            float gridHeight = GridSize * cellDisplaySize;
            
            Vector2 gridStart = new Vector2(
                gridRect.x + (gridRect.width - gridWidth) / 2 + _gridOffset.x,
                gridRect.y + (gridRect.height - gridHeight) / 2 + _gridOffset.y
            );
            
            int x = Mathf.FloorToInt((screenPos.x - gridStart.x) / cellDisplaySize);
            int y = GridSize - 1 - Mathf.FloorToInt((screenPos.y - gridStart.y) / cellDisplaySize);
            
            if (x >= 0 && x < GridSize && y >= 0 && y < GridSize)
            {
                return new Vector2Int(x, y);
            }
            
            return null;
        }
        
        #endregion
        
        #region Grid Operations
        
        private void HandleGridClick(Vector2Int pos)
        {
            bool isPaint = _currentTool == EditorTool.Paint;
            
            switch (_currentLayer)
            {
                case EditorLayer.Ground:
                    if (isPaint) AddFloorTile(pos);
                    else RemoveFloorTile(pos);
                    break;
                    
                case EditorLayer.Walls:
                    if (isPaint) AddWall(pos);
                    else RemoveWall(pos);
                    break;
                    
                case EditorLayer.Barriers:
                    if (isPaint) AddBarrier(pos, _selectedColor);
                    else RemoveBarrier(pos);
                    break;
                    
                case EditorLayer.Pickups:
                    if (isPaint) AddPickup(pos, _selectedColor);
                    else RemovePickup(pos);
                    break;
                    
                case EditorLayer.Barrels:
                    if (isPaint) AddBarrel(pos, _selectedColor);
                    else RemoveBarrel(pos);
                    break;
                    
                case EditorLayer.Bots:
                    if (isPaint) AddBot(pos);
                    else RemoveBot(pos);
                    break;
                    
                case EditorLayer.BotPaths:
                    HandleBotPathClick(pos);
                    break;
                    
                case EditorLayer.Portals:
                    if (isPaint) AddPortal(pos);
                    else RemovePortal(pos);
                    break;
                    
                case EditorLayer.HiddenAreas:
                    if (isPaint) AddHiddenBlock(pos);
                    else RemoveHiddenBlock(pos);
                    break;
            }
        }
        
        private void HandleBotPathClick(Vector2Int pos)
        {
            // Check if clicking on a bot
            var bot = _workingCopy.Bots.Find(b => b.StartPosition == pos);
            if (bot != null)
            {
                _selectedBotForPath = bot;
                _currentBotPath.Clear();
                
                if (bot.InlineWaypoints != null)
                {
                    _currentBotPath = new List<Vector2Int>(bot.InlineWaypoints);
                }
                else
                {
                    _currentBotPath.Add(pos);
                }
                return;
            }
            
            // If we have a selected bot, add waypoint
            if (_selectedBotForPath != null)
            {
                _currentBotPath.Add(pos);
            }
            else
            {
                // No bot selected - just add to current path anyway
                _currentBotPath.Add(pos);
            }
        }
        
        private void PerformFill()
        {
            int minX = Mathf.Min(_fillStartPos.x, _fillEndPos.x);
            int maxX = Mathf.Max(_fillStartPos.x, _fillEndPos.x);
            int minY = Mathf.Min(_fillStartPos.y, _fillEndPos.y);
            int maxY = Mathf.Max(_fillStartPos.y, _fillEndPos.y);
            
            // Create batch action
            var action = new LevelEditorAction(_currentLayer, ActionType.BatchAdd, 
                $"Fill {(maxX - minX + 1)}x{(maxY - minY + 1)} area");
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    action.AddElement(new ActionData(pos, _selectedColor));
                    
                    // Add element without recording individual actions
                    AddElementAtPositionNoUndo(pos);
                }
            }
            
            RecordAction(action);
            MarkDirty();
        }
        
        private void AddElementAtPositionNoUndo(Vector2Int pos)
        {
            switch (_currentLayer)
            {
                case EditorLayer.Ground:
                    if (!_workingCopy.FloorTiles.Contains(pos))
                        _workingCopy.FloorTiles.Add(pos);
                    break;
                    
                case EditorLayer.Walls:
                    if (!_workingCopy.WallTiles.Contains(pos))
                        _workingCopy.WallTiles.Add(pos);
                    break;
                    
                case EditorLayer.Barriers:
                    var existingBarrier = _workingCopy.Barriers.Find(b => b.Position == pos);
                    if (existingBarrier != null)
                        existingBarrier.Color = _selectedColor;
                    else
                        _workingCopy.Barriers.Add(new BarrierDataSnapshot { Position = pos, Color = _selectedColor });
                    break;
                    
                case EditorLayer.Pickups:
                    var existingPickup = _workingCopy.Pickups.Find(p => p.Position == pos);
                    if (existingPickup != null)
                        existingPickup.Color = _selectedColor;
                    else
                        _workingCopy.Pickups.Add(new PickupDataSnapshot { Position = pos, Color = _selectedColor, PickupId = Guid.NewGuid().ToString() });
                    break;
                    
                case EditorLayer.Barrels:
                    var existingBarrel = _workingCopy.Barrels.Find(b => b.Position == pos);
                    if (existingBarrel != null)
                        existingBarrel.Color = _selectedColor;
                    else
                        _workingCopy.Barrels.Add(new BarrelDataSnapshot { Position = pos, Color = _selectedColor, BarrelId = Guid.NewGuid().ToString() });
                    break;
                    
                case EditorLayer.HiddenAreas:
                    if (!_workingCopy.HiddenBlocks.Any(h => h.Position == pos))
                        _workingCopy.HiddenBlocks.Add(new HiddenBlockDataSnapshot { Position = pos, BlockId = Guid.NewGuid().ToString() });
                    break;
            }
        }
        
        #endregion
        
        #region Layer Add/Remove Operations
        
        private void AddFloorTile(Vector2Int pos)
        {
            if (_workingCopy.FloorTiles.Contains(pos)) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Ground, ActionType.Add)
            {
                Elements = { new ActionData(pos) }
            });
            
            _workingCopy.FloorTiles.Add(pos);
            MarkDirty();
        }
        
        private void RemoveFloorTile(Vector2Int pos)
        {
            if (!_workingCopy.FloorTiles.Contains(pos)) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Ground, ActionType.Remove)
            {
                Elements = { new ActionData(pos) }
            });
            
            _workingCopy.FloorTiles.Remove(pos);
            MarkDirty();
        }
        
        private void AddWall(Vector2Int pos)
        {
            if (_workingCopy.WallTiles.Contains(pos)) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Walls, ActionType.Add)
            {
                Elements = { new ActionData(pos) }
            });
            
            _workingCopy.WallTiles.Add(pos);
            MarkDirty();
        }
        
        private void RemoveWall(Vector2Int pos)
        {
            if (!_workingCopy.WallTiles.Contains(pos)) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Walls, ActionType.Remove)
            {
                Elements = { new ActionData(pos) }
            });
            
            _workingCopy.WallTiles.Remove(pos);
            MarkDirty();
        }
        
        private void AddBarrier(Vector2Int pos, ColorType color)
        {
            var existing = _workingCopy.Barriers.Find(b => b.Position == pos);
            
            if (existing != null)
            {
                if (existing.Color == color) return;
                
                RecordAction(new LevelEditorAction(EditorLayer.Barriers, ActionType.Modify)
                {
                    Elements = { new ActionData(pos, existing.Color) }
                });
                
                existing.Color = color;
            }
            else
            {
                RecordAction(new LevelEditorAction(EditorLayer.Barriers, ActionType.Add)
                {
                    Elements = { new ActionData(pos, color) }
                });
                
                _workingCopy.Barriers.Add(new BarrierDataSnapshot { Position = pos, Color = color });
            }
            
            MarkDirty();
        }
        
        private void RemoveBarrier(Vector2Int pos)
        {
            var existing = _workingCopy.Barriers.Find(b => b.Position == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Barriers, ActionType.Remove)
            {
                Elements = { new ActionData(pos, existing.Color) }
            });
            
            _workingCopy.Barriers.Remove(existing);
            MarkDirty();
        }
        
        private void AddPickup(Vector2Int pos, ColorType color)
        {
            var existing = _workingCopy.Pickups.Find(p => p.Position == pos);
            
            if (existing != null)
            {
                if (existing.Color == color) return;
                
                RecordAction(new LevelEditorAction(EditorLayer.Pickups, ActionType.Modify)
                {
                    Elements = { new ActionData(pos, existing.Color) { Id = existing.PickupId } }
                });
                
                existing.Color = color;
            }
            else
            {
                string id = Guid.NewGuid().ToString();
                RecordAction(new LevelEditorAction(EditorLayer.Pickups, ActionType.Add)
                {
                    Elements = { new ActionData(pos, color) { Id = id } }
                });
                
                _workingCopy.Pickups.Add(new PickupDataSnapshot { Position = pos, Color = color, PickupId = id });
            }
            
            MarkDirty();
        }
        
        private void RemovePickup(Vector2Int pos)
        {
            var existing = _workingCopy.Pickups.Find(p => p.Position == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Pickups, ActionType.Remove)
            {
                Elements = { new ActionData(pos, existing.Color) { Id = existing.PickupId } }
            });
            
            _workingCopy.Pickups.Remove(existing);
            MarkDirty();
        }
        
        private void AddBarrel(Vector2Int pos, ColorType color)
        {
            var existing = _workingCopy.Barrels.Find(b => b.Position == pos);
            
            if (existing != null)
            {
                if (existing.Color == color) return;
                
                RecordAction(new LevelEditorAction(EditorLayer.Barrels, ActionType.Modify)
                {
                    Elements = { new ActionData(pos, existing.Color) { Id = existing.BarrelId } }
                });
                
                existing.Color = color;
            }
            else
            {
                string id = Guid.NewGuid().ToString();
                RecordAction(new LevelEditorAction(EditorLayer.Barrels, ActionType.Add)
                {
                    Elements = { new ActionData(pos, color) { Id = id } }
                });
                
                _workingCopy.Barrels.Add(new BarrelDataSnapshot { Position = pos, Color = color, BarrelId = id });
            }
            
            MarkDirty();
        }
        
        private void RemoveBarrel(Vector2Int pos)
        {
            var existing = _workingCopy.Barrels.Find(b => b.Position == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Barrels, ActionType.Remove)
            {
                Elements = { new ActionData(pos, existing.Color) { Id = existing.BarrelId } }
            });
            
            _workingCopy.Barrels.Remove(existing);
            MarkDirty();
        }
        
        private void AddBot(Vector2Int pos)
        {
            var existing = _workingCopy.Bots.Find(b => b.StartPosition == pos);
            if (existing != null) return;
            
            string id = string.IsNullOrEmpty(_currentBotId) ? Guid.NewGuid().ToString() : _currentBotId;
            
            RecordAction(new LevelEditorAction(EditorLayer.Bots, ActionType.Add)
            {
                Elements = { new ActionData(pos, _selectedColor) { Id = id } }
            });
            
            _workingCopy.Bots.Add(new BotDataSnapshot
            {
                BotId = id,
                StartPosition = pos,
                InitialColor = _selectedColor,
                PathMode = _currentPathMode,
                InlineWaypoints = new List<Vector2Int> { pos }
            });
            
            MarkDirty();
        }
        
        private void RemoveBot(Vector2Int pos)
        {
            var existing = _workingCopy.Bots.Find(b => b.StartPosition == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Bots, ActionType.Remove)
            {
                Elements = { new ActionData(pos, existing.InitialColor) { Id = existing.BotId } }
            });
            
            if (_selectedBotForPath == existing)
            {
                _selectedBotForPath = null;
                _currentBotPath.Clear();
            }
            
            _workingCopy.Bots.Remove(existing);
            MarkDirty();
        }
        
        private void AddPortal(Vector2Int pos)
        {
            var existing = _workingCopy.Portals.Find(p => p.Position == pos);
            
            if (existing == null)
            {
                string id = Guid.NewGuid().ToString();
                RecordAction(new LevelEditorAction(EditorLayer.Portals, ActionType.Add)
                {
                    Elements = { new ActionData(pos) { Id = id, IsCheckpoint = _isCheckpoint } }
                });
                
                var portal = new PortalDataSnapshot
                {
                    PortalId = id,
                    Position = pos,
                    IsCheckpoint = _isCheckpoint
                };
                _workingCopy.Portals.Add(portal);
                
                // Select for linking
                if (_selectedPortalA == null)
                    _selectedPortalA = portal;
                else if (_selectedPortalB == null)
                    _selectedPortalB = portal;
                
                MarkDirty();
            }
            else
            {
                // Toggle selection for linking
                if (_selectedPortalA == null)
                    _selectedPortalA = existing;
                else if (_selectedPortalA == existing)
                    _selectedPortalA = null;
                else if (_selectedPortalB == null)
                    _selectedPortalB = existing;
                else if (_selectedPortalB == existing)
                    _selectedPortalB = null;
            }
        }
        
        private void RemovePortal(Vector2Int pos)
        {
            var existing = _workingCopy.Portals.Find(p => p.Position == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.Portals, ActionType.Remove)
            {
                Elements = { new ActionData(pos) { Id = existing.PortalId, IsCheckpoint = existing.IsCheckpoint } }
            });
            
            if (_selectedPortalA == existing) _selectedPortalA = null;
            if (_selectedPortalB == existing) _selectedPortalB = null;
            
            _workingCopy.Portals.Remove(existing);
            MarkDirty();
        }
        
        private void AddHiddenBlock(Vector2Int pos)
        {
            var existing = _workingCopy.HiddenBlocks.Find(h => h.Position == pos);
            if (existing != null) return;
            
            string id = Guid.NewGuid().ToString();
            RecordAction(new LevelEditorAction(EditorLayer.HiddenAreas, ActionType.Add)
            {
                Elements = { new ActionData(pos) { Id = id } }
            });
            
            _workingCopy.HiddenBlocks.Add(new HiddenBlockDataSnapshot { Position = pos, BlockId = id });
            MarkDirty();
        }
        
        private void RemoveHiddenBlock(Vector2Int pos)
        {
            var existing = _workingCopy.HiddenBlocks.Find(h => h.Position == pos);
            if (existing == null) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.HiddenAreas, ActionType.Remove)
            {
                Elements = { new ActionData(pos) { Id = existing.BlockId } }
            });
            
            _workingCopy.HiddenBlocks.Remove(existing);
            MarkDirty();
        }
        
        #endregion
        
        #region Selection Operations
        
        private void ClearSelection()
        {
            _selection.Clear();
            _isDraggingSelection = false;
        }
        
        private void DeleteSelection()
        {
            if (_selection.Count == 0) return;
            
            var action = new LevelEditorAction(EditorLayer.Ground, ActionType.BatchRemove, $"Delete {_selection.Count} items");
            
            foreach (var item in _selection)
            {
                action.AddElement(new ActionData(item.Position));
                DeleteItemAtPositionNoUndo(item.Position, item.Layer);
            }
            
            RecordAction(action);
            ClearSelection();
            MarkDirty();
        }
        
        private void DeleteItemAtPosition(Vector2Int pos, EditorLayer layer)
        {
            switch (layer)
            {
                case EditorLayer.Ground: RemoveFloorTile(pos); break;
                case EditorLayer.Walls: RemoveWall(pos); break;
                case EditorLayer.Barriers: RemoveBarrier(pos); break;
                case EditorLayer.Pickups: RemovePickup(pos); break;
                case EditorLayer.Barrels: RemoveBarrel(pos); break;
                case EditorLayer.Bots: RemoveBot(pos); break;
                case EditorLayer.Portals: RemovePortal(pos); break;
                case EditorLayer.HiddenAreas: RemoveHiddenBlock(pos); break;
            }
        }
        
        private void DeleteItemAtPositionNoUndo(Vector2Int pos, EditorLayer layer)
        {
            switch (layer)
            {
                case EditorLayer.Ground: _workingCopy.FloorTiles.Remove(pos); break;
                case EditorLayer.Walls: _workingCopy.WallTiles.Remove(pos); break;
                case EditorLayer.Barriers: _workingCopy.Barriers.RemoveAll(b => b.Position == pos); break;
                case EditorLayer.Pickups: _workingCopy.Pickups.RemoveAll(p => p.Position == pos); break;
                case EditorLayer.Barrels: _workingCopy.Barrels.RemoveAll(b => b.Position == pos); break;
                case EditorLayer.Bots: _workingCopy.Bots.RemoveAll(b => b.StartPosition == pos); break;
                case EditorLayer.Portals: _workingCopy.Portals.RemoveAll(p => p.Position == pos); break;
                case EditorLayer.HiddenAreas: _workingCopy.HiddenBlocks.RemoveAll(h => h.Position == pos); break;
            }
        }
        
        private void MoveSelection(Vector2Int offset)
        {
            if (offset == Vector2Int.zero) return;
            
            var action = new LevelEditorAction(EditorLayer.Ground, ActionType.Move, $"Move {_selection.Count} items");
            
            foreach (var item in _selection)
            {
                Vector2Int newPos = item.Position + offset;
                
                if (newPos.x < 0 || newPos.x >= GridSize || newPos.y < 0 || newPos.y >= GridSize)
                    continue;
                
                action.AddElement(new ActionData(item.Position) { NewPosition = newPos });
                MoveItemNoUndo(item.Position, newPos, item.Layer);
                item.Position = newPos;
            }
            
            RecordAction(action);
            MarkDirty();
        }
        
        private void MoveItemNoUndo(Vector2Int from, Vector2Int to, EditorLayer layer)
        {
            switch (layer)
            {
                case EditorLayer.Ground:
                    _workingCopy.FloorTiles.Remove(from);
                    if (!_workingCopy.FloorTiles.Contains(to))
                        _workingCopy.FloorTiles.Add(to);
                    break;
                    
                case EditorLayer.Walls:
                    _workingCopy.WallTiles.Remove(from);
                    if (!_workingCopy.WallTiles.Contains(to))
                        _workingCopy.WallTiles.Add(to);
                    break;
                    
                case EditorLayer.Barriers:
                    var barrier = _workingCopy.Barriers.Find(b => b.Position == from);
                    if (barrier != null) barrier.Position = to;
                    break;
                    
                case EditorLayer.Pickups:
                    var pickup = _workingCopy.Pickups.Find(p => p.Position == from);
                    if (pickup != null) pickup.Position = to;
                    break;
                    
                case EditorLayer.Barrels:
                    var barrel = _workingCopy.Barrels.Find(b => b.Position == from);
                    if (barrel != null) barrel.Position = to;
                    break;
                    
                case EditorLayer.Bots:
                    var bot = _workingCopy.Bots.Find(b => b.StartPosition == from);
                    if (bot != null)
                    {
                        bot.StartPosition = to;
                        // Also update first waypoint if it matches start position
                        if (bot.InlineWaypoints != null && bot.InlineWaypoints.Count > 0 && bot.InlineWaypoints[0] == from)
                        {
                            bot.InlineWaypoints[0] = to;
                        }
                    }
                    break;
                    
                case EditorLayer.Portals:
                    var portal = _workingCopy.Portals.Find(p => p.Position == from);
                    if (portal != null) portal.Position = to;
                    break;
                    
                case EditorLayer.HiddenAreas:
                    var hidden = _workingCopy.HiddenBlocks.Find(h => h.Position == from);
                    if (hidden != null) hidden.Position = to;
                    break;
            }
        }
        
        private void ApplyColorToSelection(ColorType color)
        {
            var action = new LevelEditorAction(EditorLayer.Barriers, ActionType.Modify, "Change selection color");
            
            foreach (var item in _selection)
            {
                switch (item.Layer)
                {
                    case EditorLayer.Barriers:
                        var barrier = _workingCopy.Barriers.Find(b => b.Position == item.Position);
                        if (barrier != null)
                        {
                            action.AddElement(new ActionData(item.Position, barrier.Color));
                            barrier.Color = color;
                        }
                        break;
                        
                    case EditorLayer.Pickups:
                        var pickup = _workingCopy.Pickups.Find(p => p.Position == item.Position);
                        if (pickup != null)
                        {
                            action.AddElement(new ActionData(item.Position, pickup.Color));
                            pickup.Color = color;
                        }
                        break;
                        
                    case EditorLayer.Barrels:
                        var barrel = _workingCopy.Barrels.Find(b => b.Position == item.Position);
                        if (barrel != null)
                        {
                            action.AddElement(new ActionData(item.Position, barrel.Color));
                            barrel.Color = color;
                        }
                        break;
                }
            }
            
            if (action.Elements.Count > 0)
            {
                RecordAction(action);
                MarkDirty();
            }
        }
        
        #endregion
        
        #region Bot Path Operations
        
        private void ApplyPathToSelectedBot()
        {
            if (_selectedBotForPath == null || _currentBotPath.Count < 2) return;
            
            RecordAction(new LevelEditorAction(EditorLayer.BotPaths, ActionType.Modify, "Apply bot path"));
            
            _selectedBotForPath.InlineWaypoints = new List<Vector2Int>(_currentBotPath);
            _selectedBotForPath.PathMode = _currentPathMode;
            
            _currentBotPath.Clear();
            MarkDirty();
            
            Debug.Log($"[LevelEditor] Applied path with {_selectedBotForPath.InlineWaypoints.Count} waypoints to bot {_selectedBotForPath.BotId}");
        }
        
        #endregion
        
        #region Portal Operations
        
        private void CreatePortalLink()
        {
            if (_selectedPortalA == null || _selectedPortalB == null) return;
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Portal Link",
                "PortalLink",
                "asset",
                "Save the portal link asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                var link = ScriptableObject.CreateInstance<EntranceExitLink>();
                link.linkId = Guid.NewGuid().ToString();
                link.levelA = _currentLevel;
                link.portalIdA = _selectedPortalA.PortalId;
                link.levelB = _currentLevel;
                link.portalIdB = _selectedPortalB.PortalId;
                link.isCheckpointA = _selectedPortalA.IsCheckpoint;
                link.isCheckpointB = _selectedPortalB.IsCheckpoint;
                
                AssetDatabase.CreateAsset(link, path);
                AssetDatabase.SaveAssets();
                
                _selectedPortalA.Link = link;
                _selectedPortalB.Link = link;
                
                _selectedPortalA = null;
                _selectedPortalB = null;
                
                MarkDirty();
                
                Debug.Log($"[LevelEditor] Created portal link at {path}");
            }
        }
        
        private void CreateCrossLevelPortalLink()
        {
            if (_selectedPortalA == null || _destinationLevel == null || _destinationPortalIndex < 0) return;
            
            var destPortal = _destinationLevel.portalLayer.portals[_destinationPortalIndex];
            
            // Generate a meaningful default name
            string defaultName = $"Link_{_currentLevel.levelName}_to_{_destinationLevel.levelName}";
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Cross-Level Portal Link",
                defaultName,
                "asset",
                "Save the portal link asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                var link = ScriptableObject.CreateInstance<EntranceExitLink>();
                link.linkId = Guid.NewGuid().ToString();
                link.displayName = $"{_currentLevel.levelName} <-> {_destinationLevel.levelName}";
                link.levelA = _currentLevel;
                link.portalIdA = _selectedPortalA.PortalId;
                link.levelB = _destinationLevel;
                link.portalIdB = destPortal.portalId;
                link.isCheckpointA = _selectedPortalA.IsCheckpoint;
                link.isCheckpointB = destPortal.isCheckpoint;
                
                AssetDatabase.CreateAsset(link, path);
                AssetDatabase.SaveAssets();
                
                // Assign link to source portal in working copy
                _selectedPortalA.Link = link;
                
                // Also assign to destination portal in the destination level data (directly, not via snapshot)
                destPortal.link = link;
                EditorUtility.SetDirty(_destinationLevel);
                AssetDatabase.SaveAssets();
                
                MarkDirty();
                
                // Clear selection
                _selectedPortalA = null;
                _destinationLevel = null;
                _destinationPortalIndex = -1;
                
                Debug.Log($"[LevelEditor] Created cross-level portal link at {path}");
                EditorUtility.DisplayDialog("Portal Link Created", 
                    $"Cross-level portal link created successfully!\n\nRemember to SAVE both levels for the changes to take effect.", 
                    "OK");
            }
        }
        
        private void SetPlayerSpawn(Vector2Int pos)
        {
            _workingCopy.PlayerSpawnPosition = pos;
            MarkDirty();
        }
        
        #endregion
        
        #region Undo/Redo System
        
        private void RecordAction(LevelEditorAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear();
            
            // Limit stack size
            while (_undoStack.Count > MaxUndoSteps)
            {
                var temp = new Stack<LevelEditorAction>();
                while (_undoStack.Count > 1)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Pop(); // Remove oldest
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }
        }
        
        private void PerformUndo()
        {
            if (_undoStack.Count == 0) return;
            
            var action = _undoStack.Pop();
            _redoStack.Push(action);
            
            // Reverse the action
            foreach (var element in action.Elements)
            {
                switch (action.Type)
                {
                    case ActionType.Add:
                    case ActionType.BatchAdd:
                        DeleteItemAtPositionNoUndo(element.Position, action.Layer);
                        break;
                        
                    case ActionType.Remove:
                    case ActionType.BatchRemove:
                        RestoreElement(element, action.Layer);
                        break;
                        
                    case ActionType.Modify:
                        // Restore previous color
                        RestoreElementColor(element, action.Layer);
                        break;
                        
                    case ActionType.Move:
                        if (element.NewPosition.HasValue)
                        {
                            MoveItemNoUndo(element.NewPosition.Value, element.Position, action.Layer);
                        }
                        break;
                }
            }
            
            MarkDirty();
            Repaint();
        }
        
        private void PerformRedo()
        {
            if (_redoStack.Count == 0) return;
            
            var action = _redoStack.Pop();
            _undoStack.Push(action);
            
            // Re-apply the action
            foreach (var element in action.Elements)
            {
                switch (action.Type)
                {
                    case ActionType.Add:
                    case ActionType.BatchAdd:
                        RestoreElement(element, action.Layer);
                        break;
                        
                    case ActionType.Remove:
                    case ActionType.BatchRemove:
                        DeleteItemAtPositionNoUndo(element.Position, action.Layer);
                        break;
                        
                    case ActionType.Modify:
                        // The color was stored as "previous color", so we need to swap it
                        // This is a simplification - in a full implementation, we'd store both old and new values
                        break;
                        
                    case ActionType.Move:
                        if (element.NewPosition.HasValue)
                        {
                            MoveItemNoUndo(element.Position, element.NewPosition.Value, action.Layer);
                        }
                        break;
                }
            }
            
            MarkDirty();
            Repaint();
        }
        
        private void RestoreElement(ActionData element, EditorLayer layer)
        {
            switch (layer)
            {
                case EditorLayer.Ground:
                    if (!_workingCopy.FloorTiles.Contains(element.Position))
                        _workingCopy.FloorTiles.Add(element.Position);
                    break;
                    
                case EditorLayer.Walls:
                    if (!_workingCopy.WallTiles.Contains(element.Position))
                        _workingCopy.WallTiles.Add(element.Position);
                    break;
                    
                case EditorLayer.Barriers:
                    _workingCopy.Barriers.Add(new BarrierDataSnapshot 
                    { 
                        Position = element.Position, 
                        Color = element.Color ?? ColorType.Red 
                    });
                    break;
                    
                case EditorLayer.Pickups:
                    _workingCopy.Pickups.Add(new PickupDataSnapshot 
                    { 
                        Position = element.Position, 
                        Color = element.Color ?? ColorType.Red,
                        PickupId = element.Id ?? Guid.NewGuid().ToString()
                    });
                    break;
                    
                case EditorLayer.Barrels:
                    _workingCopy.Barrels.Add(new BarrelDataSnapshot 
                    { 
                        Position = element.Position, 
                        Color = element.Color ?? ColorType.Red,
                        BarrelId = element.Id ?? Guid.NewGuid().ToString()
                    });
                    break;
                    
                case EditorLayer.Portals:
                    _workingCopy.Portals.Add(new PortalDataSnapshot 
                    { 
                        Position = element.Position,
                        PortalId = element.Id ?? Guid.NewGuid().ToString(),
                        IsCheckpoint = element.IsCheckpoint ?? false
                    });
                    break;
                    
                case EditorLayer.HiddenAreas:
                    _workingCopy.HiddenBlocks.Add(new HiddenBlockDataSnapshot 
                    { 
                        Position = element.Position,
                        BlockId = element.Id ?? Guid.NewGuid().ToString()
                    });
                    break;
            }
        }
        
        private void RestoreElementColor(ActionData element, EditorLayer layer)
        {
            if (!element.Color.HasValue) return;
            
            switch (layer)
            {
                case EditorLayer.Barriers:
                    var barrier = _workingCopy.Barriers.Find(b => b.Position == element.Position);
                    if (barrier != null) barrier.Color = element.Color.Value;
                    break;
                    
                case EditorLayer.Pickups:
                    var pickup = _workingCopy.Pickups.Find(p => p.Position == element.Position);
                    if (pickup != null) pickup.Color = element.Color.Value;
                    break;
                    
                case EditorLayer.Barrels:
                    var barrel = _workingCopy.Barrels.Find(b => b.Position == element.Position);
                    if (barrel != null) barrel.Color = element.Color.Value;
                    break;
            }
        }
        
        #endregion
        
        #region Level Management
        
        private void LoadLevelIntoWorkingCopy()
        {
            if (_currentLevel == null)
            {
                _workingCopy = null;
                return;
            }
            
            _workingCopy = LevelDataSnapshot.CreateFrom(_currentLevel);
            _hasUnsavedChanges = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selection.Clear();
            _currentBotPath.Clear();
            _selectedBotForPath = null;
            _selectedPortalA = null;
            _selectedPortalB = null;
            
            Repaint();
        }
        
        private void MarkDirty()
        {
            _hasUnsavedChanges = true;
        }
        
        private void CreateNewLevel()
        {
            if (_hasUnsavedChanges)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. Save before creating a new level?",
                    "Save", "Don't Save", "Cancel"
                );
                
                if (choice == 0) SaveLevel();
                else if (choice == 2) return;
            }
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Level Data",
                "NewLevel",
                "asset",
                "Create a new level data asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                var level = ScriptableObject.CreateInstance<LevelData>();
                level.levelName = System.IO.Path.GetFileNameWithoutExtension(path);
                level.levelId = Guid.NewGuid().ToString();
                
                // Initialize layers
                level.groundLayer = new GroundLayer();
                level.wallLayer = new WallLayer();
                level.barrierLayer = new BarrierLayer();
                level.pickupLayer = new PickupLayer();
                level.barrelLayer = new BarrelLayer();
                level.botLayer = new BotLayer();
                level.portalLayer = new PortalLayer();
                level.hiddenAreaLayer = new HiddenAreaLayer();
                
                AssetDatabase.CreateAsset(level, path);
                AssetDatabase.SaveAssets();
                
                _currentLevel = level;
                LoadLevelIntoWorkingCopy();
                
                Debug.Log($"[LevelEditor] Created new level: {level.levelName}");
            }
        }
        
        private void SaveLevel()
        {
            if (_currentLevel == null || _workingCopy == null) return;
            
            // Apply working copy to actual asset
            _workingCopy.ApplyTo(_currentLevel);
            
            EditorUtility.SetDirty(_currentLevel);
            AssetDatabase.SaveAssets();
            
            _hasUnsavedChanges = false;
            
            Debug.Log($"[LevelEditor] Saved level: {_currentLevel.levelName}");
        }
        
        #endregion
    }

    #region Enums
    
    public enum EditorLayer
    {
        Ground,
        Walls,
        Barriers,
        Pickups,
        Barrels,
        Bots,
        BotPaths,
        Portals,
        HiddenAreas
    }

    public enum EditorTool
    {
        Paint,
        Erase,
        Select,
        Fill
    }
    
    #endregion
}
