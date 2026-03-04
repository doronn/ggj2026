using Ggj2026.Game.Networking.Runtime;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Persistence;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.Navigation;
using Ggj2026.Game.Shell.World;
using Zenject;

namespace Ggj2026.Game.Shell.Installers
{
    public class GameBootstrapInstaller : MonoInstaller
    {
        public RuntimeGameConfig runtimeGameConfig;

        public override void InstallBindings()
        {
            if (runtimeGameConfig != null)
            {
                Container.BindInstance(runtimeGameConfig).AsSingle();
            }

            Container.Bind<SceneNavigator>().AsSingle();
            Container.Bind<IWorldGenerator>().To<DeterministicHexWorldGenerator>().AsSingle();
            Container.Bind<IWorldRepository>().To<JsonWorldRepository>().AsSingle();
            Container.Bind<ICollisionResolver>().To<StubCollisionResolver>().AsSingle();
            Container.Bind<SessionBootstrapService>().AsSingle();
            Container.Bind<LocalWorldSessionService>().AsSingle();
        }
    }
}
