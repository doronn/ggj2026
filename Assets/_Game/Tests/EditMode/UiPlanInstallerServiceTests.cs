using Ggj2026.Game.Authoring.Editor;
using Ggj2026.Game.Authoring.UI;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class UiPlanInstallerServiceTests
    {
        [Test]
        public void GetOrCreateManifest_ContainsExpectedSceneBindings()
        {
            UiPlanManifest manifest = UiPlanInstallerService.GetOrCreateManifest();

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.runtimePanels.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(manifest.sceneBindings.Exists(binding => binding.scenePath == "Assets/Scenes/PersistentMainMenu.unity"), Is.True);
            Assert.That(manifest.sceneBindings.Exists(binding => binding.scenePath == "Assets/Scenes/ClientWorld.unity"), Is.True);
        }
    }
}
