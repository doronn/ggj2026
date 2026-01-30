using UnityEngine;
using UnityEditor;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Help popup window for the Level Editor.
    /// Contains comprehensive documentation and usage instructions.
    /// </summary>
    public class LevelEditorHelpWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private int _currentSection;
        
        private static readonly string[] SectionNames = 
        {
            "Overview",
            "Layers",
            "Tools",
            "Bot Paths",
            "Portals",
            "Keyboard Shortcuts",
            "Tips & Best Practices"
        };
        
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorHelpWindow>("Level Editor Help");
            window.minSize = new Vector2(500, 400);
            window.maxSize = new Vector2(700, 800);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Left sidebar - navigation
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Label("Contents", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            for (int i = 0; i < SectionNames.Length; i++)
            {
                GUI.backgroundColor = _currentSection == i ? Color.cyan : Color.white;
                if (GUILayout.Button(SectionNames[i], GUILayout.Height(25)))
                {
                    _currentSection = i;
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
            
            // Right content area
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            switch (_currentSection)
            {
                case 0: DrawOverviewSection(); break;
                case 1: DrawLayersSection(); break;
                case 2: DrawToolsSection(); break;
                case 3: DrawBotPathsSection(); break;
                case 4: DrawPortalsSection(); break;
                case 5: DrawShortcutsSection(); break;
                case 6: DrawTipsSection(); break;
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawOverviewSection()
        {
            GUILayout.Label("Level Editor Overview", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "The Level Editor provides a visual grid-based interface for creating and editing game levels. " +
                "The editor uses a layer-based system where different game elements are organized into separate layers.",
                MessageType.Info);
            
            GUILayout.Space(10);
            GUILayout.Label("Window Layout", EditorStyles.boldLabel);
            DrawParagraph(
                "The editor is divided into three panels:\n\n" +
                "- Left Panel: Layer selection, tools, and save options\n" +
                "- Center Panel: The visual grid where you edit the level\n" +
                "- Right Panel: Properties for the selected layer/element and statistics");
            
            GUILayout.Space(10);
            GUILayout.Label("Getting Started", EditorStyles.boldLabel);
            DrawParagraph(
                "1. Create a new level or load an existing one using the Level Data field\n" +
                "2. Select a layer to edit from the layer list\n" +
                "3. Choose a tool (Paint, Erase, Select, or Fill)\n" +
                "4. Click or drag on the grid to place/remove elements\n" +
                "5. Save your changes with the Save button or Ctrl+S");
            
            GUILayout.Space(10);
            GUILayout.Label("Navigation", EditorStyles.boldLabel);
            DrawParagraph(
                "- Scroll wheel: Zoom in/out (0.25x to 3x)\n" +
                "- Middle mouse drag: Pan the grid view\n" +
                "- The zoom level is displayed in the top-left corner");
        }
        
        private void DrawLayersSection()
        {
            GUILayout.Label("Layer System", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            DrawParagraph(
                "Levels are composed of multiple layers, each containing different types of game elements. " +
                "You can toggle layer visibility using the eye icon next to each layer.");
            
            GUILayout.Space(10);
            
            DrawLayerInfo("Ground (Floor)", new Color(0.3f, 0.3f, 0.35f),
                "The walkable floor tiles. Players and bots can move on these tiles.");
            
            DrawLayerInfo("Walls", Color.white,
                "Solid, impassable barriers. Players and bots cannot pass through walls.");
            
            DrawLayerInfo("Barriers", Color.red,
                "Color-coded barriers that can be passed through when the player has the matching mask. " +
                "Select a color from the color picker before placing.");
            
            DrawLayerInfo("Pickups", new Color(1f, 0.5f, 0.5f),
                "Collectible masks that allow players to pass through matching barriers. " +
                "Each pickup has a unique ID for save system tracking.");
            
            DrawLayerInfo("Barrels", new Color(0.7f, 0.2f, 0.2f),
                "Exploding barrels that can be triggered. Color determines the explosion type.");
            
            DrawLayerInfo("Bots", Color.cyan,
                "Enemy bots that patrol paths. Configure their starting color and path mode.");
            
            DrawLayerInfo("Bot Paths", Color.yellow,
                "Edit waypoint paths for bots. See the Bot Paths section for details.");
            
            DrawLayerInfo("Portals", new Color(0f, 0.8f, 1f),
                "Entrance/exit points that can be linked together. Orange indicates checkpoints.");
            
            DrawLayerInfo("Hidden Areas", new Color(0.25f, 0.25f, 0.25f),
                "Secret areas that are concealed from the player until discovered.");
        }
        
        private void DrawLayerInfo(string name, Color color, string description)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Color swatch
            Rect swatchRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
            EditorGUI.DrawRect(swatchRect, color);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, swatchRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y + swatchRect.height - 1, swatchRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, 1, swatchRect.height), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x + swatchRect.width - 1, swatchRect.y, 1, swatchRect.height), Color.black);
            
            GUILayout.Space(10);
            
            EditorGUILayout.BeginVertical();
            GUILayout.Label(name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
        
        private void DrawToolsSection()
        {
            GUILayout.Label("Editing Tools", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("Paint Tool (1)", EditorStyles.boldLabel);
            DrawParagraph(
                "Click or drag to add elements to the grid. For colored elements (barriers, pickups, barrels), " +
                "select a color from the color picker first.");
            
            GUILayout.Space(5);
            GUILayout.Label("Erase Tool (2)", EditorStyles.boldLabel);
            DrawParagraph(
                "Click or drag to remove elements from the grid. Only affects elements on the currently selected layer.");
            
            GUILayout.Space(5);
            GUILayout.Label("Select Tool (3)", EditorStyles.boldLabel);
            DrawParagraph(
                "Click to select individual elements. Shift+click to add to selection.\n\n" +
                "With selection:\n" +
                "- Drag to move selected elements\n" +
                "- Edit properties in the right panel\n" +
                "- Press Delete to remove\n" +
                "- Press Escape to clear selection");
            
            GUILayout.Space(5);
            GUILayout.Label("Fill Tool (4)", EditorStyles.boldLabel);
            DrawParagraph(
                "Drag to select a rectangular area, then release to fill it with the current element type. " +
                "Useful for quickly creating floors or walls.");
        }
        
        private void DrawBotPathsSection()
        {
            GUILayout.Label("Bot Path Editing", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "Bot paths define the patrol routes that bots follow. Each bot can have either inline waypoints " +
                "or reference a shared BotPathData asset.",
                MessageType.Info);
            
            GUILayout.Space(10);
            GUILayout.Label("Creating a Bot Path", EditorStyles.boldLabel);
            DrawParagraph(
                "1. Place a bot on the Bots layer first\n" +
                "2. Switch to the Bot Paths layer\n" +
                "3. Click on the bot's position to start the path\n" +
                "4. Click additional grid cells to add waypoints\n" +
                "5. Click 'Apply Path to Selected Bot' to save the path");
            
            GUILayout.Space(10);
            GUILayout.Label("Path Modes", EditorStyles.boldLabel);
            DrawParagraph(
                "- Loop: Bot returns to the first waypoint after reaching the last\n" +
                "- PingPong: Bot reverses direction at each end\n" +
                "- OneWay: Bot stops at the final waypoint");
            
            GUILayout.Space(10);
            GUILayout.Label("Visual Indicators", EditorStyles.boldLabel);
            DrawParagraph(
                "- Cyan lines: Existing bot paths\n" +
                "- Yellow lines: Path currently being edited\n" +
                "- Dashed line: Loop connection (last to first waypoint)\n" +
                "- Numbered waypoints: Order of path traversal\n" +
                "- Red outline: Bots without paths (warning)");
        }
        
        private void DrawPortalsSection()
        {
            GUILayout.Label("Portal System", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "Portals are entrance/exit points that can be linked together to create level transitions or teleportation.",
                MessageType.Info);
            
            GUILayout.Space(10);
            GUILayout.Label("Creating Portals", EditorStyles.boldLabel);
            DrawParagraph(
                "1. Select the Portals layer\n" +
                "2. Set 'Is Checkpoint' if this portal should save progress\n" +
                "3. Click on the grid to place a portal\n" +
                "4. Portals appear teal (normal) or orange (checkpoint)");
            
            GUILayout.Space(10);
            GUILayout.Label("Linking Portals", EditorStyles.boldLabel);
            DrawParagraph(
                "1. Click on the first portal to select it (Portal A)\n" +
                "2. Click on the second portal to select it (Portal B)\n" +
                "3. Click 'Create Link' to generate an EntranceExitLink asset\n" +
                "4. The link is automatically assigned to both portals");
            
            GUILayout.Space(10);
            GUILayout.Label("Player Spawn", EditorStyles.boldLabel);
            DrawParagraph(
                "The player spawn position is shown as a green cell. Set it by right-clicking on a portal " +
                "and selecting 'Set as Player Spawn'.");
        }
        
        private void DrawShortcutsSection()
        {
            GUILayout.Label("Keyboard Shortcuts", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            DrawShortcut("Ctrl/Cmd + Z", "Undo");
            DrawShortcut("Ctrl/Cmd + Shift + Z", "Redo");
            DrawShortcut("Ctrl/Cmd + Y", "Redo (alternative)");
            DrawShortcut("Ctrl/Cmd + S", "Save level");
            DrawShortcut("1", "Paint tool");
            DrawShortcut("2", "Erase tool");
            DrawShortcut("3", "Select tool");
            DrawShortcut("4", "Fill tool");
            DrawShortcut("Escape", "Clear selection / Cancel operation");
            DrawShortcut("Delete", "Delete selected items");
            DrawShortcut("Scroll Wheel", "Zoom in/out");
            DrawShortcut("Middle Mouse Drag", "Pan view");
        }
        
        private void DrawShortcut(string keys, string action)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(keys, EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.Label(action);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTipsSection()
        {
            GUILayout.Label("Tips & Best Practices", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("General Tips", EditorStyles.boldLabel);
            DrawParagraph(
                "- Use layer visibility toggles to focus on specific elements\n" +
                "- Use the Fill tool for large floor or wall areas\n" +
                "- Save frequently to avoid losing work\n" +
                "- Check the statistics panel to track element counts");
            
            GUILayout.Space(10);
            GUILayout.Label("Performance", EditorStyles.boldLabel);
            DrawParagraph(
                "- Levels with many elements may have slower editor performance\n" +
                "- Consider splitting very large levels into multiple connected areas\n" +
                "- Use the Legend panel to understand what each color represents");
            
            GUILayout.Space(10);
            GUILayout.Label("Testing", EditorStyles.boldLabel);
            DrawParagraph(
                "- Always test your levels in Play mode after making changes\n" +
                "- Verify that bots can complete their paths\n" +
                "- Ensure all barriers have corresponding pickups\n" +
                "- Test portal links work correctly");
            
            GUILayout.Space(10);
            GUILayout.Label("Common Issues", EditorStyles.boldLabel);
            DrawParagraph(
                "- Bot path not working? Ensure the first waypoint is at the bot's position\n" +
                "- Portal not linking? Check both portals are selected before creating link\n" +
                "- Changes not saving? Click the Save button or press Ctrl+S\n" +
                "- Element not appearing? Check layer visibility is enabled");
        }
        
        private void DrawParagraph(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
        }
    }
}
