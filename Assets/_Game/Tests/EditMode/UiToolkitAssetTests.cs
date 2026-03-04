using System.IO;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class UiToolkitAssetTests
    {
        [TestCase("Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml", "WorldNameLabel")]
        [TestCase("Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml", "OperationsRailContainer")]
        [TestCase("Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml", "ContextInspectorCards")]
        [TestCase("Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml", "OnboardingOverlay")]
        [TestCase("Assets/_Game/UI/Runtime/UXML/PersistentMainMenu.uxml", "EnterLocalWorldButton")]
        [TestCase("Assets/_Game/UI/Editor/UXML/UiPlanIntegrationWindow.uxml", "ManifestPathLabel")]
        [TestCase("Assets/_Game/UI/Editor/UXML/ManualTestRunnerWindow.uxml", "ScenarioSelectionRow")]
        public void UxmlAssets_ContainExpectedNamedElements(string path, string expectedToken)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing UXML asset: {path}");
            string text = File.ReadAllText(path);
            Assert.That(text.Contains(expectedToken), Is.True, $"Expected token '{expectedToken}' in {path}");
        }
    }
}
