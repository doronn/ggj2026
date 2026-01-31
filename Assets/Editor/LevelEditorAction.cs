using System;
using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Core;
using BreakingHue.Level.Data;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Represents an undoable action in the Level Editor.
    /// Used for undo/redo functionality.
    /// </summary>
    public class LevelEditorAction
    {
        public EditorLayer Layer { get; set; }
        public ActionType Type { get; set; }
        public List<ActionData> Elements { get; set; } = new List<ActionData>();
        public string Description { get; set; }
        
        public LevelEditorAction(EditorLayer layer, ActionType type, string description = null)
        {
            Layer = layer;
            Type = type;
            Description = description ?? $"{type} on {layer}";
        }
        
        public void AddElement(ActionData data)
        {
            Elements.Add(data);
        }
    }
    
    /// <summary>
    /// Types of actions that can be undone/redone.
    /// </summary>
    public enum ActionType
    {
        Add,
        Remove,
        Modify,
        Move,
        BatchAdd,
        BatchRemove
    }
    
    /// <summary>
    /// Data for a single element affected by an action.
    /// </summary>
    public class ActionData
    {
        public Vector2Int Position { get; set; }
        public Vector2Int? NewPosition { get; set; } // For move operations
        public ColorType? Color { get; set; }
        public ColorType? NewColor { get; set; } // For color modification undo/redo
        public string Id { get; set; }
        public bool? IsCheckpoint { get; set; }
        public bool? NewIsCheckpoint { get; set; } // For portal checkpoint modification
        public bool? IsEndGame { get; set; } // For portal end game configuration
        public bool? NewIsEndGame { get; set; } // For portal end game modification
        public EndGameConfig EndGameConfig { get; set; } // For portal end game configuration
        public EndGameConfig NewEndGameConfig { get; set; } // For portal end game modification
        public PathMode? PathMode { get; set; } // For bot path mode
        public PathMode? NewPathMode { get; set; } // For bot path mode modification
        public object ExtraData { get; set; }
        
        public ActionData(Vector2Int position)
        {
            Position = position;
        }
        
        public ActionData(Vector2Int position, ColorType color) : this(position)
        {
            Color = color;
        }
        
        public ActionData Clone()
        {
            return new ActionData(Position)
            {
                NewPosition = NewPosition,
                Color = Color,
                NewColor = NewColor,
                Id = Id,
                IsCheckpoint = IsCheckpoint,
                NewIsCheckpoint = NewIsCheckpoint,
                IsEndGame = IsEndGame,
                NewIsEndGame = NewIsEndGame,
                EndGameConfig = EndGameConfig,
                NewEndGameConfig = NewEndGameConfig,
                PathMode = PathMode,
                NewPathMode = NewPathMode,
                ExtraData = ExtraData
            };
        }
    }
}
