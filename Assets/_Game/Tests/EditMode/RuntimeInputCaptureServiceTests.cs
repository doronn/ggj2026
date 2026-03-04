using Ggj2026.Game.Shell.UI;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class RuntimeInputCaptureServiceTests
    {
        [Test]
        public void RepeatedPointerNotifications_DoNotLatchWorldBlocking()
        {
            var gameObject = new GameObject("RuntimeInputCaptureServiceTests");
            var service = gameObject.AddComponent<RuntimeInputCaptureService>();

            try
            {
                service.ClearRegisteredElements();
                service.NotifyPointerEnter();
                service.NotifyPointerEnter();

                Assert.That(service.IsWorldInputBlocked(), Is.True);

                service.NotifyPointerLeave();

                Assert.That(service.IsWorldInputBlocked(), Is.False);

                service.NotifyPointerDown();
                service.NotifyPointerDown();

                Assert.That(service.IsWorldInputBlocked(), Is.True);

                service.NotifyPointerUp();

                Assert.That(service.IsWorldInputBlocked(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
