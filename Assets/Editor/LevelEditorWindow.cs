using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BreakingHue.Core;
using BreakingHue.Level.Data;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Unity Editor window for creating and editing levels.
    /// Provides a visual grid-based editor with layer support.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // Constants
        private const int GridSize = 32;
        private const float CellSize = 16f; // Visual size in editor
        private const float ToolbarHeight = 60f;
        private const float LayerPanelWidth = 200f;
        private const float PropertiesPanelWidth = 250f;

        // State
        private LevelData _currentLevel;
        private Vector2 _scrollPosition;
        private Vector2 _gridOffset = Vector2.zero;
        private float _zoom = 1f;
        
        // Tool state
        private EditorLayer _currentLayer = EditorLayer.Ground;
        private EditorTool _currentTool = EditorTool.Paint;
        private ColorType _selectedColor = ColorType.Red;
        private bool _isCheckpoint;
        
        // Bot path editing
        private List<Vector2Int> _currentBotPath = new List<Vector2Int>();
        private string _currentBotId;
        private PathMode _currentPathMode = PathMode.Loop;
        
        // Portal linking
        private PortalData _selectedPortalA;
        private PortalData _selectedPortalB;
        
        // Styles
        private GUIStyle _gridCellStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/Breaking Hue/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            // Reset state
            _stylesInitialized = false;
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

        private void OnGUI()
        {
            InitializeStyles();
            
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

        private void DrawLayerPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LayerPanelWidth));
            
            GUILayout.Label("Layers", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Level asset field
            EditorGUI.BeginChangeCheck();
            _currentLevel = (LevelData)EditorGUILayout.ObjectField("Level Data", _currentLevel, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck() && _currentLevel != null)
            {
                Repaint();
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
                // Layer buttons
                DrawLayerButton(EditorLayer.Ground, "Ground (Floor)");
                DrawLayerButton(EditorLayer.Walls, "Walls");
                DrawLayerButton(EditorLayer.Barriers, "Barriers");
                DrawLayerButton(EditorLayer.Pickups, "Pickups");
                DrawLayerButton(EditorLayer.Barrels, "Barrels");
                DrawLayerButton(EditorLayer.Bots, "Bots");
                DrawLayerButton(EditorLayer.BotPaths, "Bot Paths");
                DrawLayerButton(EditorLayer.Portals, "Portals");
                DrawLayerButton(EditorLayer.HiddenAreas, "Hidden Areas");
                
                GUILayout.Space(10);
                
                // Tools
                GUILayout.Label("Tools", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                DrawToolButton(EditorTool.Paint, "Paint");
                DrawToolButton(EditorTool.Erase, "Erase");
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                DrawToolButton(EditorTool.Select, "Select");
                DrawToolButton(EditorTool.Fill, "Fill");
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                
                // Save button
                if (GUILayout.Button("Save Level", GUILayout.Height(25)))
                {
                    SaveLevel();
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawLayerButton(EditorLayer layer, string label)
        {
            bool isSelected = _currentLayer == layer;
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
            
            if (GUILayout.Button(label, GUILayout.Height(25)))
            {
                _currentLayer = layer;
            }
            
            GUI.backgroundColor = Color.white;
        }

        private void DrawToolButton(EditorTool tool, string label)
        {
            bool isSelected = _currentTool == tool;
            GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
            
            if (GUILayout.Button(label, GUILayout.Height(20)))
            {
                _currentTool = tool;
            }
            
            GUI.backgroundColor = Color.white;
        }

        private void DrawGridPanel()
        {
            Rect gridRect = GUILayoutUtility.GetRect(
                position.width - LayerPanelWidth - PropertiesPanelWidth - 20,
                position.height - 20,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );
            
            // Background
            EditorGUI.DrawRect(gridRect, new Color(0.15f, 0.15f, 0.15f));
            
            if (_currentLevel == null)
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
                        gridStart.y + (GridSize - 1 - y) * cellDisplaySize + clipOffset.y, // Flip Y
                        cellDisplaySize - 1,
                        cellDisplaySize - 1
                    );
                    
                    // Determine cell color based on contents
                    Color cellColor = GetCellColor(x, y);
                    EditorGUI.DrawRect(cellRect, cellColor);
                    
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
            if (_currentLayer == EditorLayer.BotPaths || _currentLayer == EditorLayer.Bots)
            {
                DrawBotPaths(gridStart + clipOffset, cellDisplaySize);
            }
            
            GUI.EndClip();
            
            // Zoom indicator
            GUI.Label(new Rect(gridRect.x + 5, gridRect.y + 5, 100, 20), $"Zoom: {_zoom:F1}x", EditorStyles.miniLabel);
        }

        private Color GetCellColor(int x, int y)
        {
            Vector2Int pos = new Vector2Int(x, y);
            Color baseColor = new Color(0.2f, 0.2f, 0.2f); // Empty
            
            // Check each layer
            if (_currentLevel.groundLayer?.floorTiles?.Contains(pos) == true)
            {
                baseColor = new Color(0.3f, 0.3f, 0.35f); // Floor
            }
            
            if (_currentLevel.wallLayer?.wallTiles?.Contains(pos) == true)
            {
                return Color.white; // Wall
            }
            
            if (_currentLevel.barrierLayer?.barriers != null)
            {
                var barrier = _currentLevel.barrierLayer.barriers.Find(b => b.position == pos);
                if (barrier != null)
                {
                    Color c = barrier.color.ToColor();
                    c.a = 0.7f;
                    return Color.Lerp(baseColor, c, 0.8f);
                }
            }
            
            if (_currentLevel.pickupLayer?.pickups != null)
            {
                var pickup = _currentLevel.pickupLayer.pickups.Find(p => p.position == pos);
                if (pickup != null)
                {
                    Color c = pickup.color.ToColor();
                    c = Color.Lerp(c, Color.white, 0.3f);
                    return c;
                }
            }
            
            if (_currentLevel.barrelLayer?.barrels != null)
            {
                var barrel = _currentLevel.barrelLayer.barrels.Find(b => b.position == pos);
                if (barrel != null)
                {
                    Color c = barrel.color.ToColor();
                    c = Color.Lerp(c, Color.black, 0.3f);
                    return c;
                }
            }
            
            if (_currentLevel.botLayer?.bots != null)
            {
                var bot = _currentLevel.botLayer.bots.Find(b => b.startPosition == pos);
                if (bot != null)
                {
                    return Color.cyan;
                }
            }
            
            if (_currentLevel.portalLayer?.portals != null)
            {
                var portal = _currentLevel.portalLayer.portals.Find(p => p.position == pos);
                if (portal != null)
                {
                    return portal.isCheckpoint ? new Color(1f, 0.8f, 0f) : new Color(0f, 0.8f, 1f);
                }
            }
            
            if (_currentLevel.portalLayer?.playerSpawnPosition == pos)
            {
                return Color.green;
            }
            
            if (_currentLevel.hiddenAreaLayer?.hiddenBlocks != null)
            {
                var hidden = _currentLevel.hiddenAreaLayer.hiddenBlocks.Find(h => h.position == pos);
                if (hidden != null)
                {
                    return new Color(0.25f, 0.25f, 0.25f);
                }
            }
            
            return baseColor;
        }

        private void DrawBotPaths(Vector2 gridStart, float cellSize)
        {
            if (_currentLevel.botLayer?.bots == null) return;
            
            Handles.color = Color.cyan;
            
            foreach (var bot in _currentLevel.botLayer.bots)
            {
                var waypoints = bot.inlineWaypoints ?? bot.pathData?.waypoints;
                if (waypoints == null || waypoints.Count < 2) continue;
                
                for (int i = 0; i < waypoints.Count - 1; i++)
                {
                    Vector2 from = GridToEditor(waypoints[i], gridStart, cellSize);
                    Vector2 to = GridToEditor(waypoints[i + 1], gridStart, cellSize);
                    Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
                }
                
                // Draw loop connection if applicable
                if (bot.pathMode == PathMode.Loop && waypoints.Count > 2)
                {
                    Vector2 from = GridToEditor(waypoints[waypoints.Count - 1], gridStart, cellSize);
                    Vector2 to = GridToEditor(waypoints[0], gridStart, cellSize);
                    Handles.color = new Color(0f, 1f, 1f, 0.5f);
                    Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
                    Handles.color = Color.cyan;
                }
            }
            
            // Draw current path being edited
            if (_currentBotPath.Count > 1)
            {
                Handles.color = Color.yellow;
                for (int i = 0; i < _currentBotPath.Count - 1; i++)
                {
                    Vector2 from = GridToEditor(_currentBotPath[i], gridStart, cellSize);
                    Vector2 to = GridToEditor(_currentBotPath[i + 1], gridStart, cellSize);
                    Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
                }
            }
        }

        private Vector2 GridToEditor(Vector2Int gridPos, Vector2 gridStart, float cellSize)
        {
            return new Vector2(
                gridStart.x + gridPos.x * cellSize + cellSize / 2,
                gridStart.y + (GridSize - 1 - gridPos.y) * cellSize + cellSize / 2
            );
        }

        private void DrawPropertiesPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(PropertiesPanelWidth));
            
            GUILayout.Label("Properties", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            if (_currentLevel != null)
            {
                // Level properties
                _currentLevel.levelName = EditorGUILayout.TextField("Level Name", _currentLevel.levelName);
                _currentLevel.levelIndex = EditorGUILayout.IntField("Level Index", _currentLevel.levelIndex);
                
                GUILayout.Space(10);
                
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
                
                GUILayout.Space(10);
                
                // Statistics
                GUILayout.Label("Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Floor tiles:", _currentLevel.groundLayer?.floorTiles?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Walls:", _currentLevel.wallLayer?.wallTiles?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Barriers:", _currentLevel.barrierLayer?.barriers?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Pickups:", _currentLevel.pickupLayer?.pickups?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Barrels:", _currentLevel.barrelLayer?.barrels?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Bots:", _currentLevel.botLayer?.bots?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Portals:", _currentLevel.portalLayer?.portals?.Count.ToString() ?? "0");
            }
            
            EditorGUILayout.EndVertical();
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
        }

        private void DrawBotPathProperties()
        {
            GUILayout.Label("Bot Path Editor", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("Waypoints:", _currentBotPath.Count.ToString());
            
            if (GUILayout.Button("Clear Path"))
            {
                _currentBotPath.Clear();
            }
            
            if (_currentBotPath.Count >= 2)
            {
                if (GUILayout.Button("Apply Path to Selected Bot"))
                {
                    ApplyPathToSelectedBot();
                }
            }
            
            GUILayout.Space(5);
            GUILayout.Label("Click on grid to add waypoints", EditorStyles.miniLabel);
        }

        private void DrawPortalProperties()
        {
            GUILayout.Label("Portal Properties", EditorStyles.boldLabel);
            
            _isCheckpoint = EditorGUILayout.Toggle("Is Checkpoint", _isCheckpoint);
            
            GUILayout.Space(10);
            
            GUILayout.Label("Portal Linking", EditorStyles.boldLabel);
            
            if (_selectedPortalA != null)
            {
                EditorGUILayout.LabelField("Portal A:", _selectedPortalA.portalId);
            }
            
            if (_selectedPortalB != null)
            {
                EditorGUILayout.LabelField("Portal B:", _selectedPortalB.portalId);
            }
            
            if (_selectedPortalA != null && _selectedPortalB != null)
            {
                if (GUILayout.Button("Create Link"))
                {
                    CreatePortalLink();
                }
            }
            
            if (GUILayout.Button("Clear Selection"))
            {
                _selectedPortalA = null;
                _selectedPortalB = null;
            }
        }

        private void HandleInput()
        {
            Event e = Event.current;
            
            if (e == null || _currentLevel == null) return;
            
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
            
            // Paint/Erase with left click
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                Vector2Int? gridPos = ScreenToGrid(e.mousePosition);
                if (gridPos.HasValue)
                {
                    HandleGridClick(gridPos.Value);
                    e.Use();
                    Repaint();
                }
            }
        }

        private Vector2Int? ScreenToGrid(Vector2 screenPos)
        {
            // Calculate grid rect (simplified - should match DrawGridPanel)
            Rect gridRect = new Rect(
                LayerPanelWidth,
                0,
                position.width - LayerPanelWidth - PropertiesPanelWidth,
                position.height
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
            int y = GridSize - 1 - Mathf.FloorToInt((screenPos.y - gridStart.y) / cellDisplaySize); // Flip Y
            
            if (x >= 0 && x < GridSize && y >= 0 && y < GridSize)
            {
                return new Vector2Int(x, y);
            }
            
            return null;
        }

        private void HandleGridClick(Vector2Int pos)
        {
            bool isPaint = _currentTool == EditorTool.Paint || _currentTool == EditorTool.Fill;
            
            switch (_currentLayer)
            {
                case EditorLayer.Ground:
                    if (isPaint)
                        AddFloorTile(pos);
                    else
                        RemoveFloorTile(pos);
                    break;
                    
                case EditorLayer.Walls:
                    if (isPaint)
                        AddWall(pos);
                    else
                        RemoveWall(pos);
                    break;
                    
                case EditorLayer.Barriers:
                    if (isPaint)
                        AddBarrier(pos, _selectedColor);
                    else
                        RemoveBarrier(pos);
                    break;
                    
                case EditorLayer.Pickups:
                    if (isPaint)
                        AddPickup(pos, _selectedColor);
                    else
                        RemovePickup(pos);
                    break;
                    
                case EditorLayer.Barrels:
                    if (isPaint)
                        AddBarrel(pos, _selectedColor);
                    else
                        RemoveBarrel(pos);
                    break;
                    
                case EditorLayer.Bots:
                    if (isPaint)
                        AddBot(pos);
                    else
                        RemoveBot(pos);
                    break;
                    
                case EditorLayer.BotPaths:
                    AddWaypointToPath(pos);
                    break;
                    
                case EditorLayer.Portals:
                    if (isPaint)
                        AddPortal(pos);
                    else
                        RemovePortal(pos);
                    break;
                    
                case EditorLayer.HiddenAreas:
                    if (isPaint)
                        AddHiddenBlock(pos);
                    else
                        RemoveHiddenBlock(pos);
                    break;
            }
            
            EditorUtility.SetDirty(_currentLevel);
        }

        #region Layer Operations

        private void AddFloorTile(Vector2Int pos)
        {
            if (_currentLevel.groundLayer == null)
                _currentLevel.groundLayer = new GroundLayer();
            if (_currentLevel.groundLayer.floorTiles == null)
                _currentLevel.groundLayer.floorTiles = new List<Vector2Int>();
            
            if (!_currentLevel.groundLayer.floorTiles.Contains(pos))
                _currentLevel.groundLayer.floorTiles.Add(pos);
        }

        private void RemoveFloorTile(Vector2Int pos)
        {
            _currentLevel.groundLayer?.floorTiles?.Remove(pos);
        }

        private void AddWall(Vector2Int pos)
        {
            if (_currentLevel.wallLayer == null)
                _currentLevel.wallLayer = new WallLayer();
            if (_currentLevel.wallLayer.wallTiles == null)
                _currentLevel.wallLayer.wallTiles = new List<Vector2Int>();
            
            if (!_currentLevel.wallLayer.wallTiles.Contains(pos))
                _currentLevel.wallLayer.wallTiles.Add(pos);
        }

        private void RemoveWall(Vector2Int pos)
        {
            _currentLevel.wallLayer?.wallTiles?.Remove(pos);
        }

        private void AddBarrier(Vector2Int pos, ColorType color)
        {
            if (_currentLevel.barrierLayer == null)
                _currentLevel.barrierLayer = new BarrierLayer();
            if (_currentLevel.barrierLayer.barriers == null)
                _currentLevel.barrierLayer.barriers = new List<BarrierData>();
            
            var existing = _currentLevel.barrierLayer.barriers.Find(b => b.position == pos);
            if (existing != null)
            {
                existing.color = color;
            }
            else
            {
                _currentLevel.barrierLayer.barriers.Add(new BarrierData { position = pos, color = color });
            }
        }

        private void RemoveBarrier(Vector2Int pos)
        {
            _currentLevel.barrierLayer?.barriers?.RemoveAll(b => b.position == pos);
        }

        private void AddPickup(Vector2Int pos, ColorType color)
        {
            if (_currentLevel.pickupLayer == null)
                _currentLevel.pickupLayer = new PickupLayer();
            if (_currentLevel.pickupLayer.pickups == null)
                _currentLevel.pickupLayer.pickups = new List<PickupData>();
            
            var existing = _currentLevel.pickupLayer.pickups.Find(p => p.position == pos);
            if (existing != null)
            {
                existing.color = color;
            }
            else
            {
                _currentLevel.pickupLayer.pickups.Add(new PickupData
                {
                    position = pos,
                    color = color,
                    pickupId = Guid.NewGuid().ToString()
                });
            }
        }

        private void RemovePickup(Vector2Int pos)
        {
            _currentLevel.pickupLayer?.pickups?.RemoveAll(p => p.position == pos);
        }

        private void AddBarrel(Vector2Int pos, ColorType color)
        {
            if (_currentLevel.barrelLayer == null)
                _currentLevel.barrelLayer = new BarrelLayer();
            if (_currentLevel.barrelLayer.barrels == null)
                _currentLevel.barrelLayer.barrels = new List<BarrelData>();
            
            var existing = _currentLevel.barrelLayer.barrels.Find(b => b.position == pos);
            if (existing != null)
            {
                existing.color = color;
            }
            else
            {
                _currentLevel.barrelLayer.barrels.Add(new BarrelData
                {
                    position = pos,
                    color = color,
                    barrelId = Guid.NewGuid().ToString()
                });
            }
        }

        private void RemoveBarrel(Vector2Int pos)
        {
            _currentLevel.barrelLayer?.barrels?.RemoveAll(b => b.position == pos);
        }

        private void AddBot(Vector2Int pos)
        {
            if (_currentLevel.botLayer == null)
                _currentLevel.botLayer = new BotLayer();
            if (_currentLevel.botLayer.bots == null)
                _currentLevel.botLayer.bots = new List<BotData>();
            
            var existing = _currentLevel.botLayer.bots.Find(b => b.startPosition == pos);
            if (existing == null)
            {
                _currentLevel.botLayer.bots.Add(new BotData
                {
                    botId = string.IsNullOrEmpty(_currentBotId) ? Guid.NewGuid().ToString() : _currentBotId,
                    startPosition = pos,
                    initialColor = _selectedColor,
                    pathMode = _currentPathMode,
                    inlineWaypoints = new List<Vector2Int> { pos }
                });
            }
        }

        private void RemoveBot(Vector2Int pos)
        {
            _currentLevel.botLayer?.bots?.RemoveAll(b => b.startPosition == pos);
        }

        private void AddWaypointToPath(Vector2Int pos)
        {
            _currentBotPath.Add(pos);
        }

        private void ApplyPathToSelectedBot()
        {
            if (_currentLevel.botLayer?.bots == null || _currentBotPath.Count < 2) return;
            
            // Find the first bot that starts at the first waypoint
            var bot = _currentLevel.botLayer.bots.Find(b => b.startPosition == _currentBotPath[0]);
            if (bot != null)
            {
                bot.inlineWaypoints = new List<Vector2Int>(_currentBotPath);
                bot.pathMode = _currentPathMode;
                _currentBotPath.Clear();
                EditorUtility.SetDirty(_currentLevel);
            }
        }

        private void AddPortal(Vector2Int pos)
        {
            if (_currentLevel.portalLayer == null)
                _currentLevel.portalLayer = new PortalLayer();
            if (_currentLevel.portalLayer.portals == null)
                _currentLevel.portalLayer.portals = new List<PortalData>();
            
            var existing = _currentLevel.portalLayer.portals.Find(p => p.position == pos);
            if (existing == null)
            {
                var portal = new PortalData
                {
                    portalId = Guid.NewGuid().ToString(),
                    position = pos,
                    isCheckpoint = _isCheckpoint
                };
                _currentLevel.portalLayer.portals.Add(portal);
                
                // Select for linking
                if (_selectedPortalA == null)
                    _selectedPortalA = portal;
                else if (_selectedPortalB == null)
                    _selectedPortalB = portal;
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
            var portal = _currentLevel.portalLayer?.portals?.Find(p => p.position == pos);
            if (portal != null)
            {
                if (_selectedPortalA == portal) _selectedPortalA = null;
                if (_selectedPortalB == portal) _selectedPortalB = null;
                _currentLevel.portalLayer.portals.Remove(portal);
            }
        }

        private void CreatePortalLink()
        {
            if (_selectedPortalA == null || _selectedPortalB == null) return;
            
            // Create EntranceExitLink asset
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
                link.portalIdA = _selectedPortalA.portalId;
                link.levelB = _currentLevel; // Same level for now
                link.portalIdB = _selectedPortalB.portalId;
                link.isCheckpointA = _selectedPortalA.isCheckpoint;
                link.isCheckpointB = _selectedPortalB.isCheckpoint;
                
                AssetDatabase.CreateAsset(link, path);
                AssetDatabase.SaveAssets();
                
                // Assign link to portals
                _selectedPortalA.link = link;
                _selectedPortalB.link = link;
                
                EditorUtility.SetDirty(_currentLevel);
                
                _selectedPortalA = null;
                _selectedPortalB = null;
            }
        }

        private void AddHiddenBlock(Vector2Int pos)
        {
            if (_currentLevel.hiddenAreaLayer == null)
                _currentLevel.hiddenAreaLayer = new HiddenAreaLayer();
            if (_currentLevel.hiddenAreaLayer.hiddenBlocks == null)
                _currentLevel.hiddenAreaLayer.hiddenBlocks = new List<HiddenBlockData>();
            
            var existing = _currentLevel.hiddenAreaLayer.hiddenBlocks.Find(h => h.position == pos);
            if (existing == null)
            {
                _currentLevel.hiddenAreaLayer.hiddenBlocks.Add(new HiddenBlockData
                {
                    position = pos,
                    blockId = Guid.NewGuid().ToString()
                });
            }
        }

        private void RemoveHiddenBlock(Vector2Int pos)
        {
            _currentLevel.hiddenAreaLayer?.hiddenBlocks?.RemoveAll(h => h.position == pos);
        }

        #endregion

        private void CreateNewLevel()
        {
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
            }
        }

        private void SaveLevel()
        {
            if (_currentLevel != null)
            {
                EditorUtility.SetDirty(_currentLevel);
                AssetDatabase.SaveAssets();
                Debug.Log($"[LevelEditor] Saved level: {_currentLevel.levelName}");
            }
        }
    }

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
}
