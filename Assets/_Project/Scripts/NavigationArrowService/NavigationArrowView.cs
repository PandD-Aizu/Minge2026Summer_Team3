using UnityEngine;

namespace View
{
    public class NavigationArrowView : MonoBehaviour
    {
        [SerializeField] private Transform _arrowTransform;
        [SerializeField, Min(0f)] private float _radius = 1.5f;
        [SerializeField] private float _heightOffset;

        public bool CanUpdate => isActiveAndEnabled && _arrowTransform != null;

        /// <summary>プレイヤーと目的地の間に矢印を配置し、ローカルZ軸の先端を目的地へ向ける</summary>
        /// <param name="playerPosition">表示の中心となるプレイヤーのワールド座標</param>
        /// <param name="direction">XZ平面上の単位ベクトル、ゼロなら非表示</param>
        /// <param name="distanceToTarget">目的地までの水平距離、省略時は通常の表示半径を使う</param>
        /// <example>PresenterからShowDirection(playerPosition, direction, distance)を呼ぶ</example>
        public void ShowDirection(Vector3 playerPosition, Vector3 direction, float distanceToTarget = float.PositiveInfinity)
        {
            // 水平位置が一致するときは方向を表示しない
            bool visible = direction.sqrMagnitude > 0f;
            _arrowTransform.gameObject.SetActive(visible);
            if (!visible) return;

            // プレイヤーの回転とは独立したワールド座標で配置する
            // 目的地へ近づいても矢印の中心が目的地を追い越さないようにする
            float radius = Mathf.Min(_radius, Mathf.Max(0f, distanceToTarget) * 0.5f);
            Vector3 position = playerPosition + direction * radius + Vector3.up * _heightOffset;
            _arrowTransform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        /// <summary>Viewを無効にしたときに矢印も非表示にする</summary>
        /// <example>コンポーネントのチェックを外したときにUnityが呼ぶ</example>
        private void OnDisable()
        {
            if (_arrowTransform != null) _arrowTransform.gameObject.SetActive(false);
        }
    }
}
