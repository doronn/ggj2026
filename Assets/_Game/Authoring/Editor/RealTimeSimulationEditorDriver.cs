using System;
using Ggj2026.Game.Shell.World;
using UnityEditor;

namespace Ggj2026.Game.Authoring.Editor
{
    [InitializeOnLoad]
    public static class RealTimeSimulationEditorDriver
    {
        private const double StaleHeartbeatSeconds = 0.25d;
        private static long _lastEditorPumpUtcTicks;

        static RealTimeSimulationEditorDriver()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            var controller = RealTimeSimulationController.Active;
            long nowUtcTicks = DateTime.UtcNow.Ticks;

            if (!TryComputeFallbackDelta(
                    EditorApplication.isPlaying,
                    EditorApplication.isPaused,
                    controller,
                    nowUtcTicks,
                    ref _lastEditorPumpUtcTicks,
                    out float deltaSeconds))
            {
                return;
            }

            controller.ProcessRealtimeStep(deltaSeconds);
        }

        public static bool TryComputeFallbackDelta(
            bool isPlaying,
            bool isPaused,
            RealTimeSimulationController controller,
            long nowUtcTicks,
            ref long lastEditorPumpUtcTicks,
            out float deltaSeconds)
        {
            deltaSeconds = 0f;

            if (!isPlaying || isPaused || controller == null || !controller.IsRunning)
            {
                lastEditorPumpUtcTicks = 0L;
                return false;
            }

            long heartbeatTicks = controller.LastPlayerLoopHeartbeatUtcTicks;
            if (heartbeatTicks <= 0L)
            {
                lastEditorPumpUtcTicks = 0L;
                return false;
            }

            double secondsSinceHeartbeat = TimeSpan.FromTicks(Math.Max(0L, nowUtcTicks - heartbeatTicks)).TotalSeconds;
            if (secondsSinceHeartbeat <= StaleHeartbeatSeconds)
            {
                lastEditorPumpUtcTicks = 0L;
                return false;
            }

            if (lastEditorPumpUtcTicks <= 0L)
            {
                lastEditorPumpUtcTicks = nowUtcTicks;
                return false;
            }

            deltaSeconds = (float)TimeSpan.FromTicks(Math.Max(0L, nowUtcTicks - lastEditorPumpUtcTicks)).TotalSeconds;
            lastEditorPumpUtcTicks = nowUtcTicks;
            return deltaSeconds > 0.0001f;
        }
    }
}
