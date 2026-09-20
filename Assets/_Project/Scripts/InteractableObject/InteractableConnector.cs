using System.Collections.Generic;
using R3;
using UnityEngine;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>操作対象の範囲にいるプレイヤーを検出する</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public class InteractableConnector : MonoBehaviour
    {
        private readonly HashSet<Collider> _playerColliders = new();
        private readonly Subject<bool> _playerNearbyChanged = new();
        private SphereCollider _trigger;
        private System.Predicate<Collider> _isUnavailable;

        public Observable<bool> OnPlayerNearbyChanged => _playerNearbyChanged;
        public bool IsPlayerNearby { get; private set; }

        /// <summary>接近判定を物理的に押し返さないトリガーとして初期化する</summary>
        /// <example>受付付近に付け、SphereColliderのRadiusで反応距離を調整する</example>
        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _isUnavailable = IsUnavailable;

            // CharacterControllerのプレイヤーともトリガーイベントを受け取れるようにする
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        /// <summary>範囲へ入ったプレイヤーのColliderを登録する</summary>
        /// <param name="other">範囲に入ったCollider</param>
        /// <example>主人公が受付へ近づいたときにUnityが呼ぶ</example>
        private void OnTriggerEnter(Collider other) => TrackPlayer(other);

        /// <summary>範囲内で再有効化した場合にもプレイヤーを検出する</summary>
        /// <param name="other">範囲内に留まっているCollider</param>
        /// <example>対象を有効化した時点で主人公が近くにいる場合にも使用する</example>
        private void OnTriggerStay(Collider other) => TrackPlayer(other);

        /// <summary>最後のプレイヤーColliderが範囲から出たときに非表示を通知する</summary>
        /// <param name="other">範囲から出たCollider</param>
        /// <example>複数Colliderのうち一部の退出だけでは非表示にしない</example>
        private void OnTriggerExit(Collider other)
        {
            if (_playerColliders.Remove(other)) SetPlayerNearby(_playerColliders.Count > 0);
        }

        /// <summary>プレイヤーだけを登録し、接近状態が変化したときに通知する</summary>
        /// <param name="other">プレイヤーかどうかを調べるCollider</param>
        /// <example>EnterとStayから共通の登録処理として呼ぶ</example>
        private void TrackPlayer(Collider other)
        {
            if (!isActiveAndEnabled || _trigger == null || !_trigger.enabled || _playerColliders.Contains(other)) return;

            // 既存Player PrefabはUntaggedなので、実際の入力コンポーネントで識別する
            var player = other.GetComponentInParent<PlayerInputReader>();
            if (player == null || !player.isActiveAndEnabled) return;

            _playerColliders.Add(other);
            SetPlayerNearby(true);
        }

        /// <summary>Exitが届かない無効化や瞬間移動も検知する</summary>
        /// <example>主人公を範囲外へワープさせたときもマークを消す</example>
        private void LateUpdate()
        {
            if (_trigger == null || !_trigger.enabled)
            {
                _playerColliders.Clear();
            }
            else
            {
                _playerColliders.RemoveWhere(_isUnavailable);
            }

            SetPlayerNearby(_playerColliders.Count > 0);
        }

        /// <summary>接近判定から除外すべきColliderかを調べる</summary>
        /// <param name="collider">登録済みのプレイヤーCollider</param>
        /// <returns>破棄済み、無効、または範囲外ならtrue</returns>
        /// <example>HashSet.RemoveWhereへ渡して無効な参照を取り除く</example>
        private bool IsUnavailable(Collider collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return true;
            var player = collider.GetComponentInParent<PlayerInputReader>();
            if (player == null || !player.isActiveAndEnabled) return true;

            // Colliderを無効化してワープした場合はExitが届かないため、実際の重なりも確認する
            return !Physics.ComputePenetration(
                _trigger, _trigger.transform.position, _trigger.transform.rotation,
                collider, collider.transform.position, collider.transform.rotation,
                out _, out _);
        }

        /// <summary>接近状態の変化だけを購読者へ通知する</summary>
        /// <param name="nearby">範囲内に有効なプレイヤーがいればtrue</param>
        /// <example>Stayが繰り返されても同じ表示通知を再発行しない</example>
        private void SetPlayerNearby(bool nearby)
        {
            if (IsPlayerNearby == nearby) return;
            IsPlayerNearby = nearby;
            _playerNearbyChanged.OnNext(nearby);
        }

        /// <summary>判定を無効にしたときに登録とマークを解除する</summary>
        /// <example>操作できなくなった対象はConnectorを無効化する</example>
        private void OnDisable()
        {
            _playerColliders.Clear();
            SetPlayerNearby(false);
        }

        /// <summary>対象の破棄時に通知ストリームを解放する</summary>
        /// <example>シーンを切り替えたときにUnityが呼ぶ</example>
        private void OnDestroy() => _playerNearbyChanged.Dispose();
    }
}
