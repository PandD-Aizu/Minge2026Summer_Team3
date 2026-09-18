using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace SceneLoadServices
{
	public class SceneLoadService
	{
        /// <summary>
        /// Addressableにて登録したシーンを非同期で呼び出す
        /// </summary>
        /// <param name="addressableSceneName">Addressableで登録したアドレス名</param>
        /// <returns>シーンの呼び出しに成功したかどうか</returns>
        public async UniTask<bool> LoadSceneAsync(string addressableSceneName)
        {
            var handle = Addressables.LoadSceneAsync(addressableSceneName);

            try
            {
                await handle.ToUniTask();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);

                if (handle.IsValid())
                    Addressables.Release(handle);

                return false;
            }
        }
	}
}
