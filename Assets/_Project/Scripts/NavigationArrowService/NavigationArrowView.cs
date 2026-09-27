using UnityEngine;

namespace View
{
    public class NavigationArrowView : MonoBehaviour
    {
        [SerializeField] private Transform _arrowTransform;
        [SerializeField, Min(0f)] private float _radius = 1.5f;
        [SerializeField] private float _heightOffset;

        public bool CanUpdate => isActiveAndEnabled && _arrowTransform != null;

        /// <summary>プレイヤーの周囲にCubeを配置し、目的地方向へ向ける</summary>
        /// <param name="playerPosition">表示の中心となるプレイヤーのワールド座標</param>
        /// <param name="direction">XZ平面上の単位ベクトル、ゼロなら非表示</param>
        /// <example>PresenterからShowDirection(playerPosition, direction)を呼ぶ</example>
        public void ShowDirection(Vector3 playerPosition, Vector3 direction)
        {
            // 水平位置が一致するときは方向を表示しない
            bool visible = direction.sqrMagnitude > 0f;
            _arrowTransform.gameObject.SetActive(visible);
            if (!visible) return;

            // プレイヤーの回転とは独立したワールド座標で配置する
            Vector3 position = playerPosition + direction * _radius + Vector3.up * _heightOffset;
            _arrowTransform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        /// <summary>Viewを無効にしたときにCubeも非表示にする</summary>
        /// <example>コンポーネントのチェックを外したときにUnityが呼ぶ</example>
        private void OnDisable()
        {
            if (_arrowTransform != null) _arrowTransform.gameObject.SetActive(false);
        }
    }
}
