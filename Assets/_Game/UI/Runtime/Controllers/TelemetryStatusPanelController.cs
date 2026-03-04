using UnityEngine.UIElements;

namespace Ggj2026.Game.UI.Runtime.Controllers
{
    public sealed class TelemetryStatusPanelController
    {
        private readonly Label _telemetryLabel;
        private readonly Label _statusLabel;

        public TelemetryStatusPanelController(Label telemetryLabel, Label statusLabel)
        {
            _telemetryLabel = telemetryLabel;
            _statusLabel = statusLabel;
        }

        public void Refresh(string telemetrySummary, string statusSummary)
        {
            if (_telemetryLabel != null)
            {
                _telemetryLabel.text = telemetrySummary;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = statusSummary;
            }
        }
    }
}
