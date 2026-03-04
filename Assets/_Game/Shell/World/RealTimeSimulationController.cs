using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shell.Config;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    public sealed class RealTimeSimulationController : MonoBehaviour
    {
        private const float MinimumSpeed = 0.25f;
        private const float MaximumSpeed = 8f;

        private RuntimeGameConfig _runtimeGameConfig;
        private LocalColonyGameplayService _gameplayService;
        private float _tickAccumulator;
        private float _speedMultiplier = 1f;
        private long _latestInterpolatedTick;
        private float _latestInterpolatedTickDurationSeconds;
        private double _lastRealtimeStepAt;
        private long _lastPlayerLoopHeartbeatUtcTicks;

        public static RealTimeSimulationController Active { get; private set; }

        public bool IsRunning { get; private set; }
        public float SpeedMultiplier => _speedMultiplier;
        public float BaseTickDurationSeconds => _runtimeGameConfig != null ? _runtimeGameConfig.RealTimeTickDurationSeconds : 1f;
        public float EffectiveTickDurationSeconds => BaseTickDurationSeconds / Mathf.Max(MinimumSpeed, _speedMultiplier);
        public long LastPlayerLoopHeartbeatUtcTicks => _lastPlayerLoopHeartbeatUtcTicks;

        public event System.Action StateChanged;

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void Update()
        {
            RecordPlayerLoopHeartbeat();
            double now = Time.realtimeSinceStartupAsDouble;
            if (_lastRealtimeStepAt <= 0d)
            {
                _lastRealtimeStepAt = now;
            }

            float deltaTime = Time.unscaledDeltaTime;
#if UNITY_EDITOR
            if (IsRunning && deltaTime <= 0.0001f)
            {
                deltaTime = (float)System.Math.Max(0d, now - _lastRealtimeStepAt);
            }
#endif
            ProcessRealtimeStep(deltaTime);
            _lastRealtimeStepAt = now;
        }

        public void Initialize(RuntimeGameConfig runtimeGameConfig, LocalColonyGameplayService gameplayService)
        {
            _runtimeGameConfig = runtimeGameConfig;
            _gameplayService = gameplayService;
            _speedMultiplier = runtimeGameConfig != null ? runtimeGameConfig.DefaultRealTimeSimulationSpeed : 1f;
            _tickAccumulator = 0f;
            _latestInterpolatedTick = 0;
            _latestInterpolatedTickDurationSeconds = 0f;
            _lastRealtimeStepAt = Time.realtimeSinceStartupAsDouble;
            RecordPlayerLoopHeartbeat();
            NotifyStateChanged();
        }

        public void ProcessRealtimeStep(float unscaledDeltaTime)
        {
            if (!IsRunning || _gameplayService == null || _gameplayService.Snapshot == null)
            {
                return;
            }

            _tickAccumulator += Mathf.Max(0f, unscaledDeltaTime) * _speedMultiplier;
            float baseTickDuration = BaseTickDurationSeconds;

            while (_tickAccumulator >= baseTickDuration)
            {
                _tickAccumulator -= baseTickDuration;
                _gameplayService.AdvanceSimulation(
                    1,
                    new SessionUiSource
                    {
                        panelId = "RealTimeSimulation",
                        elementId = "AutoTick",
                        controller = nameof(RealTimeSimulationController)
                    });
                _latestInterpolatedTick = _gameplayService.Snapshot.simulationTick;
                _latestInterpolatedTickDurationSeconds = EffectiveTickDurationSeconds;
                NotifyStateChanged();
            }
        }

        public void SetRunning(bool isRunning)
        {
            if (IsRunning == isRunning)
            {
                return;
            }

            IsRunning = isRunning;
            _tickAccumulator = 0f;
            _lastRealtimeStepAt = Time.realtimeSinceStartupAsDouble;
            RecordPlayerLoopHeartbeat();
            NotifyStateChanged();
        }

        public void ToggleRunning()
        {
            SetRunning(!IsRunning);
        }

        public void SetSpeedMultiplier(float speedMultiplier)
        {
            float clamped = Mathf.Clamp(speedMultiplier, MinimumSpeed, MaximumSpeed);
            if (Mathf.Approximately(_speedMultiplier, clamped))
            {
                return;
            }

            _speedMultiplier = clamped;
            NotifyStateChanged();
        }

        public void IncreaseSpeed()
        {
            SetSpeedMultiplier(Mathf.Min(MaximumSpeed, _speedMultiplier * 2f));
        }

        public void DecreaseSpeed()
        {
            SetSpeedMultiplier(Mathf.Max(MinimumSpeed, _speedMultiplier * 0.5f));
        }

        public bool TryGetInterpolationDurationForTick(long simulationTick, out float durationSeconds)
        {
            if (_latestInterpolatedTick == simulationTick && _latestInterpolatedTickDurationSeconds > 0f)
            {
                durationSeconds = _latestInterpolatedTickDurationSeconds;
                return true;
            }

            durationSeconds = 0f;
            return false;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void RecordPlayerLoopHeartbeat()
        {
            _lastPlayerLoopHeartbeatUtcTicks = System.DateTime.UtcNow.Ticks;
        }
    }
}
