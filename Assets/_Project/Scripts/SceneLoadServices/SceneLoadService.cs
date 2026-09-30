using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SceneLoadServices
{
	public class SceneLoadService
	{
        public bool IsLoading { get; private set; }

        /// <summary>Inspectorで選択されたSceneを非同期で読み込む</summary>
        /// <param name="scene">Addressablesに登録済みのScene参照</param>
        /// <returns>読み込みに成功した場合はtrue</returns>
        /// <example>接触式の出口からLoadSceneAsync(destination)を呼ぶ</example>
        public UniTask<bool> LoadSceneAsync(SceneReference scene)
        {
            if (scene == null || !scene.IsAssigned) return UniTask.FromResult(false);
            return LoadSceneAsync(scene.Guid);
        }

        /// <summary>
        /// Addressableにて登録したシーンを非同期で呼び出す
        /// </summary>
        /// <param name="addressableSceneName">Addressableで登録したアドレス名</param>
        /// <returns>シーンの呼び出しに成功したかどうか</returns>
        /// <example>LoadSceneAsync("CampStage")でキャンプへ移動する</example>
        public async UniTask<bool> LoadSceneAsync(string addressableSceneName)
        {
            // 複数の出口に同時接触してもシーンの読み込みは一度だけ行う
            if (IsLoading || string.IsNullOrEmpty(addressableSceneName)) return false;
            IsLoading = true;
            AsyncOperationHandle<SceneInstance> handle = default;

            try
            {
                handle = Addressables.LoadSceneAsync(addressableSceneName);
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
            finally
            {
                IsLoading = false;
            }
        }
	}
}
