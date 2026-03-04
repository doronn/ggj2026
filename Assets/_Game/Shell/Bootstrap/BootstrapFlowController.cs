using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Ggj2026.Game.Shell.Bootstrap
{
    public class BootstrapFlowController : MonoBehaviour
    {
        [SerializeField] private RuntimeGameConfig runtimeGameConfig;
        [SerializeField] private bool autoAdvanceToMainMenu = true;

        private SceneNavigator _sceneNavigator;

        public void Construct([InjectOptional] SceneNavigator sceneNavigator)
        {
            _sceneNavigator = sceneNavigator;
        }

        private void Awake()
        {
            _sceneNavigator ??= new SceneNavigator();
        }

        private async void Start()
        {
            if (!autoAdvanceToMainMenu || runtimeGameConfig == null)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != runtimeGameConfig.BootstrapSceneName)
            {
                return;
            }

            await UniTask.DelayFrame(1);
            await _sceneNavigator.LoadSingleAsync(runtimeGameConfig.MainMenuSceneName);
        }
    }
}
