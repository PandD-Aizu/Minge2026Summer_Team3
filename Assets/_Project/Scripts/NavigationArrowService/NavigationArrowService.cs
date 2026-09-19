using UnityEngine;

namespace NavigationArrowServices
{
    public class NavigationArrowService
    {
        private readonly Transform _playerTransform;

        public Vector3 PlayerPosition => _playerTransform.position;
        public Vector3 TargetPosition { get; private set; }
        public bool CanUpdate => _playerTransform != null;

        /// <summary>案内対象のプレイヤーと目的地を保持する</summary>
        /// <param name="playerTransform">案内対象のプレイヤーのTransform</param>
        /// <param name="targetPosition">目的地の初期ワールド座標</param>
        /// <example>LifetimeScopeからnew NavigationArrowService(playerTransform, targetPosition)で生成する</example>
        public NavigationArrowService(Transform playerTransform, Vector3 targetPosition)
        {
            _playerTransform = playerTransform;
            TargetPosition = targetPosition;
        }

        /// <summary>案内先の目的地を変更する</summary>
        /// <param name="targetPosition">新しい目的地のワールド座標</param>
        /// <example>SetTargetPosition(new Vector3(10f, 0f, 5f))</example>
        public void SetTargetPosition(Vector3 targetPosition)
        {
            TargetPosition = targetPosition;
        }

        /// <summary>高低差を除いた目的地方向を計算する</summary>
        /// <param name="from">プレイヤーのワールド座標</param>
        /// <returns>XZ平面上の単位ベクトル、水平位置が一致するときはゼロ</returns>
        /// <example>CalculateArrowDirection(PlayerPosition)</example>
        public Vector3 CalculateArrowDirection(Vector3 from)
        {
            // 保持している目的地への方向から高低差を取り除く
            Vector3 direction = TargetPosition - from;
            direction.y = 0f;
            return direction.normalized;
        }
    }
}
