using Zenject;
using BreakingHue.Core;
using BreakingHue.Level;
using BreakingHue.Save;
using BreakingHue.UI;
using BreakingHue.Input;
using BreakingHue.Camera;

namespace BreakingHue.Installers
{
    /// <summary>
    /// Main Zenject installer for Breaking Hue.
    /// Binds all core services and components.
    /// Updated for third-person camera system.
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

            // Game Manager for win conditions and scene transitions
            Container.Bind<GameManager>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Level Manager for level loading and transitions
            Container.Bind<LevelManager>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Checkpoint Manager for save/restore functionality
            Container.Bind<CheckpointManager>()
                .FromComponentInHierarchy()
                .AsSingle();

            // NEW: Third-person camera system bindings
            
            // Input Manager for input state and device switching
            Container.Bind<InputManager>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Game Camera for third-person follow and rotation
            Container.Bind<GameCamera>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Controls Bar Controller for contextual control hints
            Container.Bind<ControlsBarController>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Pause Menu Controller
            Container.Bind<PauseMenuController>()
                .FromComponentInHierarchy()
                .AsSingle();

            // Input Icon Provider for device-specific icons
            Container.Bind<InputIconProvider>()
                .FromComponentInHierarchy()
                .AsSingle();
        }
    }
}
