using UnityEngine.UIElements;

namespace Ggj2026.Game.UI.Runtime.Controllers
{
    public sealed class WorldInspectorPanelController
    {
        private readonly Label _worldSummaryLabel;
        private readonly Label _resourceSummaryLabel;
        private readonly Label _selectedCellLabel;
        private readonly Label _selectedEdgeLabel;

        public WorldInspectorPanelController(Label worldSummaryLabel, Label resourceSummaryLabel, Label selectedCellLabel, Label selectedEdgeLabel)
        {
            _worldSummaryLabel = worldSummaryLabel;
            _resourceSummaryLabel = resourceSummaryLabel;
            _selectedCellLabel = selectedCellLabel;
            _selectedEdgeLabel = selectedEdgeLabel;
        }

        public void Refresh(string worldSummary, string resourceSummary, string selectedCellSummary, string selectedEdgeSummary)
        {
            if (_worldSummaryLabel != null)
            {
                _worldSummaryLabel.text = worldSummary;
            }

            if (_resourceSummaryLabel != null)
            {
                _resourceSummaryLabel.text = resourceSummary;
            }

            if (_selectedCellLabel != null)
            {
                _selectedCellLabel.text = selectedCellSummary;
            }

            if (_selectedEdgeLabel != null)
            {
                _selectedEdgeLabel.text = selectedEdgeSummary;
            }
        }
    }
}
