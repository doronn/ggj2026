using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class OnboardingFlowServiceTests
    {
        private const string CompletedKey = "Ggj2026.Onboarding.Completed";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(CompletedKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(CompletedKey);
        }

        [Test]
        public void BuildViewModel_FirstSessionStartsActive()
        {
            var service = new OnboardingFlowService();

            OnboardingStepViewModel state = service.BuildViewModel();

            Assert.That(state.isActive, Is.True);
            Assert.That(state.title, Is.EqualTo("Welcome"));
            Assert.That(service.IsOperationGroupVisible("simulation"), Is.True);
            Assert.That(service.IsOperationGroupVisible("landmarks"), Is.False);
        }

        [Test]
        public void TryAdvance_ReachesFinalStepAndCompletes()
        {
            var service = new OnboardingFlowService();
            string message = string.Empty;

            while (service.BuildViewModel().isActive)
            {
                service.TryAdvance(out message);
            }

            Assert.That(service.BuildViewModel().isActive, Is.False);
            Assert.That(PlayerPrefs.GetInt(CompletedKey, 0), Is.EqualTo(1));
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void TrySkip_HidesGuideForSessionOnly()
        {
            var service = new OnboardingFlowService();

            bool success = service.TrySkip(out string message);

            Assert.That(success, Is.True);
            Assert.That(message, Is.EqualTo("Guide skipped for this session."));
            Assert.That(service.BuildViewModel().isActive, Is.False);
            Assert.That(PlayerPrefs.GetInt(CompletedKey, 0), Is.EqualTo(0));
        }

        [Test]
        public void IsInspectorTabVisible_FollowsCurrentStep()
        {
            var service = new OnboardingFlowService();
            service.TryAdvance(out _);
            service.TryAdvance(out _);

            Assert.That(service.IsInspectorTabVisible(InspectorTabKind.World), Is.True);
            Assert.That(service.IsInspectorTabVisible(InspectorTabKind.Entities), Is.False);
        }
    }
}
