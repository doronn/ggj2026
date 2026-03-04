using System.Collections;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Bootstrap;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Tests.PlayMode
{
    public class UiToolkitControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator PersistentMainMenuController_BuildsVisualTree()
        {
            var go = new GameObject("PersistentMainMenuTest");
            go.AddComponent<UIDocument>();
            var controller = go.AddComponent<PersistentMainMenuController>();

            yield return null;

            VisualElement root = go.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Button>("EnterLocalWorldButton"), Is.Not.Null);
            Assert.That(root.Q<Label>("TitleLabel"), Is.Not.Null);

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator ClientWorldHudController_BuildsHudVisualTree()
        {
            var go = new GameObject("ClientWorldHudTest");
            go.AddComponent<UIDocument>();
            var controller = go.AddComponent<ClientWorldHudController>();

            controller.Initialize(null, new WorldSnapshot(), null, null);
            yield return null;

            VisualElement root = go.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("WorldNameLabel"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("OperationsRailPanel"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("ContextInspectorPanel"), Is.Not.Null);
            Assert.That(root.Q<Label>("CurrentToolHintLabel"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("OnboardingOverlay"), Is.Not.Null);
            Assert.That(go.GetComponent<RealTimeSimulationController>(), Is.Not.Null);

            Object.Destroy(go);
        }
    }
}
