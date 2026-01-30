using System;
using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Core;
using BreakingHue.Level.Data;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Editor
{
    /// <summary>
    /// A deep copy of LevelData for non-destructive editing.
    /// Changes are made to this snapshot, then applied back to the original on save.
    /// </summary>
    public class LevelDataSnapshot
    {
        // Level metadata
        public string LevelId { get; set; }
        public string LevelName { get; set; }
        public int LevelIndex { get; set; }
        public float CellSize { get; set; }
        
        // Layer data (deep copies)
        public List<Vector2Int> FloorTiles { get; set; } = new List<Vector2Int>();
        public List<Vector2Int> WallTiles { get; set; } = new List<Vector2Int>();
        public List<BarrierDataSnapshot> Barriers { get; set; } = new List<BarrierDataSnapshot>();
        public List<PickupDataSnapshot> Pickups { get; set; } = new List<PickupDataSnapshot>();
        public List<BarrelDataSnapshot> Barrels { get; set; } = new List<BarrelDataSnapshot>();
        public List<BotDataSnapshot> Bots { get; set; } = new List<BotDataSnapshot>();
        public List<PortalDataSnapshot> Portals { get; set; } = new List<PortalDataSnapshot>();
        public List<HiddenBlockDataSnapshot> HiddenBlocks { get; set; } = new List<HiddenBlockDataSnapshot>();
        public Vector2Int PlayerSpawnPosition { get; set; }
        
        /// <summary>
        /// Creates a deep copy snapshot from a LevelData asset.
        /// </summary>
        public static LevelDataSnapshot CreateFrom(LevelData source)
        {
            if (source == null) return null;
            
            var snapshot = new LevelDataSnapshot
            {
                LevelId = source.levelId,
                LevelName = source.levelName,
                LevelIndex = source.levelIndex,
                CellSize = source.cellSize
            };
            
            // Copy floor tiles
            if (source.groundLayer?.floorTiles != null)
            {
                snapshot.FloorTiles = new List<Vector2Int>(source.groundLayer.floorTiles);
            }
            
            // Copy wall tiles
            if (source.wallLayer?.wallTiles != null)
            {
                snapshot.WallTiles = new List<Vector2Int>(source.wallLayer.wallTiles);
            }
            
            // Copy barriers
            if (source.barrierLayer?.barriers != null)
            {
                foreach (var b in source.barrierLayer.barriers)
                {
                    snapshot.Barriers.Add(new BarrierDataSnapshot
                    {
                        Position = b.position,
                        Color = b.color
                    });
                }
            }
            
            // Copy pickups
            if (source.pickupLayer?.pickups != null)
            {
                foreach (var p in source.pickupLayer.pickups)
                {
                    snapshot.Pickups.Add(new PickupDataSnapshot
                    {
                        Position = p.position,
                        Color = p.color,
                        PickupId = p.pickupId
                    });
                }
            }
            
            // Copy barrels
            if (source.barrelLayer?.barrels != null)
            {
                foreach (var b in source.barrelLayer.barrels)
                {
                    snapshot.Barrels.Add(new BarrelDataSnapshot
                    {
                        Position = b.position,
                        Color = b.color,
                        BarrelId = b.barrelId
                    });
                }
            }
            
            // Copy bots
            if (source.botLayer?.bots != null)
            {
                foreach (var b in source.botLayer.bots)
                {
                    var botSnapshot = new BotDataSnapshot
                    {
                        BotId = b.botId,
                        StartPosition = b.startPosition,
                        InitialColor = b.initialColor,
                        PathMode = b.pathMode,
                        PathData = b.pathData // Reference to ScriptableObject
                    };
                    
                    if (b.inlineWaypoints != null)
                    {
                        botSnapshot.InlineWaypoints = new List<Vector2Int>(b.inlineWaypoints);
                    }
                    
                    snapshot.Bots.Add(botSnapshot);
                }
            }
            
            // Copy portals
            if (source.portalLayer?.portals != null)
            {
                foreach (var p in source.portalLayer.portals)
                {
                    snapshot.Portals.Add(new PortalDataSnapshot
                    {
                        PortalId = p.portalId,
                        Position = p.position,
                        IsCheckpoint = p.isCheckpoint,
                        IsEntrance = p.isEntrance,
                        Link = p.link // Reference to ScriptableObject
                    });
                }
                
                snapshot.PlayerSpawnPosition = source.portalLayer.playerSpawnPosition;
            }
            
            // Copy hidden blocks
            if (source.hiddenAreaLayer?.hiddenBlocks != null)
            {
                foreach (var h in source.hiddenAreaLayer.hiddenBlocks)
                {
                    snapshot.HiddenBlocks.Add(new HiddenBlockDataSnapshot
                    {
                        Position = h.position,
                        BlockId = h.blockId
                    });
                }
            }
            
            return snapshot;
        }
        
        /// <summary>
        /// Applies this snapshot's data back to the original LevelData asset.
        /// </summary>
        public void ApplyTo(LevelData target)
        {
            if (target == null) return;
            
            target.levelName = LevelName;
            target.levelIndex = LevelIndex;
            target.cellSize = CellSize;
            
            // Apply floor tiles
            if (target.groundLayer == null)
                target.groundLayer = new GroundLayer();
            target.groundLayer.floorTiles = new List<Vector2Int>(FloorTiles);
            
            // Apply wall tiles
            if (target.wallLayer == null)
                target.wallLayer = new WallLayer();
            target.wallLayer.wallTiles = new List<Vector2Int>(WallTiles);
            
            // Apply barriers
            if (target.barrierLayer == null)
                target.barrierLayer = new BarrierLayer();
            target.barrierLayer.barriers = new List<BarrierData>();
            foreach (var b in Barriers)
            {
                target.barrierLayer.barriers.Add(new BarrierData
                {
                    position = b.Position,
                    color = b.Color
                });
            }
            
            // Apply pickups
            if (target.pickupLayer == null)
                target.pickupLayer = new PickupLayer();
            target.pickupLayer.pickups = new List<PickupData>();
            foreach (var p in Pickups)
            {
                target.pickupLayer.pickups.Add(new PickupData
                {
                    position = p.Position,
                    color = p.Color,
                    pickupId = p.PickupId
                });
            }
            
            // Apply barrels
            if (target.barrelLayer == null)
                target.barrelLayer = new BarrelLayer();
            target.barrelLayer.barrels = new List<BarrelData>();
            foreach (var b in Barrels)
            {
                target.barrelLayer.barrels.Add(new BarrelData
                {
                    position = b.Position,
                    color = b.Color,
                    barrelId = b.BarrelId
                });
            }
            
            // Apply bots
            if (target.botLayer == null)
                target.botLayer = new BotLayer();
            target.botLayer.bots = new List<BotData>();
            foreach (var b in Bots)
            {
                var bot = new BotData
                {
                    botId = b.BotId,
                    startPosition = b.StartPosition,
                    initialColor = b.InitialColor,
                    pathMode = b.PathMode,
                    pathData = b.PathData
                };
                
                if (b.InlineWaypoints != null)
                {
                    bot.inlineWaypoints = new List<Vector2Int>(b.InlineWaypoints);
                }
                
                target.botLayer.bots.Add(bot);
            }
            
            // Apply portals
            if (target.portalLayer == null)
                target.portalLayer = new PortalLayer();
            target.portalLayer.portals = new List<PortalData>();
            foreach (var p in Portals)
            {
                target.portalLayer.portals.Add(new PortalData
                {
                    portalId = p.PortalId,
                    position = p.Position,
                    isCheckpoint = p.IsCheckpoint,
                    isEntrance = p.IsEntrance,
                    link = p.Link
                });
            }
            target.portalLayer.playerSpawnPosition = PlayerSpawnPosition;
            
            // Apply hidden blocks
            if (target.hiddenAreaLayer == null)
                target.hiddenAreaLayer = new HiddenAreaLayer();
            target.hiddenAreaLayer.hiddenBlocks = new List<HiddenBlockData>();
            foreach (var h in HiddenBlocks)
            {
                target.hiddenAreaLayer.hiddenBlocks.Add(new HiddenBlockData
                {
                    position = h.Position,
                    blockId = h.BlockId
                });
            }
        }
    }
    
    // Snapshot data classes for each element type
    
    public class BarrierDataSnapshot
    {
        public Vector2Int Position { get; set; }
        public ColorType Color { get; set; }
    }
    
    public class PickupDataSnapshot
    {
        public Vector2Int Position { get; set; }
        public ColorType Color { get; set; }
        public string PickupId { get; set; }
    }
    
    public class BarrelDataSnapshot
    {
        public Vector2Int Position { get; set; }
        public ColorType Color { get; set; }
        public string BarrelId { get; set; }
    }
    
    public class BotDataSnapshot
    {
        public string BotId { get; set; }
        public Vector2Int StartPosition { get; set; }
        public ColorType InitialColor { get; set; }
        public PathMode PathMode { get; set; }
        public BotPathData PathData { get; set; }
        public List<Vector2Int> InlineWaypoints { get; set; }
    }
    
    public class PortalDataSnapshot
    {
        public string PortalId { get; set; }
        public Vector2Int Position { get; set; }
        public bool IsCheckpoint { get; set; }
        public bool IsEntrance { get; set; }
        public EntranceExitLink Link { get; set; }
    }
    
    public class HiddenBlockDataSnapshot
    {
        public Vector2Int Position { get; set; }
        public string BlockId { get; set; }
    }
}
