using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreakingHue.Gameplay.Bot
{
    /// <summary>
    /// ScriptableObject that defines a bot's movement path.
    /// Contains a list of waypoints that the bot follows.
    /// </summary>
    [CreateAssetMenu(fileName = "BotPath", menuName = "Breaking Hue/Bot Path Data")]
    public class BotPathData : ScriptableObject
    {
        [Header("Path Settings")]
        [Tooltip("List of waypoints (grid positions) for the bot to follow")]
        public List<Vector2Int> waypoints = new List<Vector2Int>();
        
        [Tooltip("How the bot moves along the path")]
        public PathMode pathMode = PathMode.Loop;
        
        [Tooltip("Movement speed multiplier")]
        [Range(0.1f, 3f)]
        public float speedMultiplier = 1f;
        
        [Tooltip("Time to wait at each waypoint (seconds)")]
        [Range(0f, 5f)]
        public float waitTimeAtWaypoints = 0f;

        /// <summary>
        /// Gets the next waypoint index based on current position and path mode.
        /// </summary>
        public int GetNextWaypointIndex(int currentIndex, bool movingForward)
        {
            if (waypoints == null || waypoints.Count == 0)
                return -1;

            switch (pathMode)
            {
                case PathMode.Loop:
                    return (currentIndex + 1) % waypoints.Count;
                    
                case PathMode.PingPong:
                    if (movingForward)
                    {
                        if (currentIndex >= waypoints.Count - 1)
                            return currentIndex - 1; // Reverse at end
                        return currentIndex + 1;
                    }
                    else
                    {
                        if (currentIndex <= 0)
                            return 1; // Reverse at start
                        return currentIndex - 1;
                    }
                    
                case PathMode.OneWay:
                    if (currentIndex >= waypoints.Count - 1)
                        return -1; // Stop at end
                    return currentIndex + 1;
                    
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Checks if the bot should reverse direction at the current waypoint (for ping-pong).
        /// </summary>
        public bool ShouldReverse(int currentIndex, bool movingForward)
        {
            if (pathMode != PathMode.PingPong)
                return false;

            if (movingForward && currentIndex >= waypoints.Count - 1)
                return true;
            if (!movingForward && currentIndex <= 0)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the world position for a waypoint index.
        /// </summary>
        public Vector3 GetWorldPosition(int waypointIndex, float cellSize, Vector3 levelOffset)
        {
            if (waypointIndex < 0 || waypointIndex >= waypoints.Count)
                return Vector3.zero;

            Vector2Int gridPos = waypoints[waypointIndex];
            return new Vector3(
                gridPos.x * cellSize + levelOffset.x + cellSize * 0.5f,
                0f,
                gridPos.y * cellSize + levelOffset.z + cellSize * 0.5f
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure we have at least 2 waypoints for meaningful movement
            if (waypoints != null && waypoints.Count == 1)
            {
                Debug.LogWarning($"[BotPathData] Path '{name}' has only 1 waypoint. Bot will stay stationary.");
            }
        }
#endif
    }

    /// <summary>
    /// Defines how a bot moves along its path.
    /// </summary>
    public enum PathMode
    {
        /// <summary>
        /// Bot loops back to start after reaching the end.
        /// </summary>
        Loop,
        
        /// <summary>
        /// Bot reverses direction at each end.
        /// </summary>
        PingPong,
        
        /// <summary>
        /// Bot stops when reaching the last waypoint.
        /// </summary>
        OneWay
    }

    /// <summary>
    /// Runtime state for bot path following.
    /// Serializable for checkpoint saving.
    /// </summary>
    [Serializable]
    public class BotPathState
    {
        public int currentWaypointIndex;
        public bool movingForward = true;
        public float waitTimer;
        public bool isWaiting;

        public BotPathState Clone()
        {
            return new BotPathState
            {
                currentWaypointIndex = currentWaypointIndex,
                movingForward = movingForward,
                waitTimer = waitTimer,
                isWaiting = isWaiting
            };
        }
    }
}
