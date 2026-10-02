using NavigationArrowServices;
using Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using View;

namespace LifetimeScopes
{
    public class NavigationArrowLifetimeScope : LifetimeScope
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private Transform _initialTarget;
        [SerializeField] private Vector3 _initialTargetPosition = new Vector3(5f, 0f, 5f);

        /// <summary>案内に必要な初期値をServiceへ渡し、表示との接続を登録する</summary>
        /// <param name="builder">シーンの依存関係の登録先</param>
        /// <example>シーン読み込み時にVContainerが呼ぶ</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // NavigationArrow関連のサービス
            // シーン上の目的地を優先し、未指定の場合は従来の座標設定を使う
            var targetPosition = _initialTarget != null ? _initialTarget.position : _initialTargetPosition;
            builder.Register(_ => new NavigationArrowService(_playerTransform, targetPosition), Lifetime.Singleton);

            // NavigationArrow関連のPresenter
            builder.RegisterEntryPoint<NavigationArrowPresenter>();

            // NavigationArrow関連のView
            builder.RegisterComponentInHierarchy<NavigationArrowView>();
        }
    }
}
