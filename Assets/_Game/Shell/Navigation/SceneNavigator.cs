using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ggj2026.Game.Shell.Navigation
{
    public sealed class SceneNavigator
    {
        public async UniTask LoadSingleAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                return;
            }

            await operation.ToUniTask();
        }
    }
}
