using Zenject;
using BreakingHue.Core;
using BreakingHue.Level;
using BreakingHue.UI;

namespace BreakingHue.Installers
{
    /// <summary>
    /// Main Zenject installer for Breaking Hue.
    /// Binds all core services and components.
    /// </summary>
    public class GameInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // Core Services (Singleton - persists across scenes)
            // MaskInventory is the slot-based inventory system
            Container.Bind<MaskInventory>()
                .AsSingle()
                .NonLazy();

            // Scene Components (FromComponentInHierarchy - finds existing instances)
            Container.Bind<LevelGenerator>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<GameHUDController>()
                .FromComponentInHierarchy()
                .AsSingle();
        }
    }
}
