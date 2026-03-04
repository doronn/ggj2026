using System.Collections;
using Ggj2026.Game.Shell.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ggj2026.Game.Tests.PlayMode
{
    public class RuntimeInputCapturePlayModeTests
    {
        [UnityTest]
        public IEnumerator PointerEvents_ToggleInputBlocking()
        {
            yield return null;

            RuntimeInputCaptureService service = RuntimeInputCaptureService.Active;
            Assert.That(service, Is.Not.Null);

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
    }
}
