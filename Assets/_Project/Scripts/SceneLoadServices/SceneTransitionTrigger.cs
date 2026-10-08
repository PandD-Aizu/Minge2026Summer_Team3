using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace SceneLoadServices
{
    /// <summary>Playerが触れると指定Sceneへ移動する出口</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class SceneTransitionTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("Addressablesに登録済みの遷移先Scene")]
        private SceneReference _destination = new();

        private SceneLoadService _sceneLoader;
        private bool _isTransitioning;

        /// <summary>共通のScene読み込みサービスを受け取る</summary>
        /// <param name="sceneLoader">RootのLifetimeScopeが管理するサービス</param>
        /// <example>出口のLifetimeScopeが登録したときに自動注入される</example>
        [Inject]
        public void Construct(SceneLoadService sceneLoader) => _sceneLoader = sceneLoader;

        /// <summary>Playerを押し返さない接触判定を設定する</summary>
        /// <example>PrefabをSceneへ配置して再生すると自動実行される</example>
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        /// <summary>有効なPlayerが接触したときだけ遷移を開始する</summary>
        /// <param name="other">出口へ入ったCollider</param>
        /// <example>PlayerのCharacterControllerが出口へ入るとUnityが呼ぶ</example>
        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || _isTransitioning) return;

            // Player PrefabはUntaggedなので入力コンポーネントで識別する
            var player = other.GetComponentInParent<PlayerInputReader>();
            if (player == null || !player.isActiveAndEnabled) return;

            if (_sceneLoader == null || _destination == null || !_destination.IsAssigned)
            {
                Debug.LogError("出口のLifetimeScopeと遷移先Sceneを設定してください", this);
                return;
            }

            if (_sceneLoader.IsLoading) return;
            TransitionAsync(player).Forget();
        }

        /// <summary>ロード中の歩行と重複接触を止め、失敗時は再接触を許可する</summary>
        /// <param name="player">出口へ接触したPlayer</param>
        /// <returns>Sceneのロードが終わるまで待機する処理</returns>
        /// <example>OnTriggerEnterから呼び、失敗した場合は出口へ入り直す</example>
        private async UniTask TransitionAsync(PlayerInputReader player)
        {
            _isTransitioning = true;

            // 読み込み待ちの間に背景の外へ歩いてしまうことを防ぐ
            using (player.BlockMovement())
            {
                bool loaded = await _sceneLoader.LoadSceneAsync(_destination);
                if (this != null && !loaded) _isTransitioning = false;
            }
        }
    }
}
