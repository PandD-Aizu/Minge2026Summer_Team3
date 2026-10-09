using NavigationArrowServices;
using Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using View;

namespace LifetimeScopes
{
    /// <summary>旧矢印のシーン参照を引き継ぎ、光る経路の案内を登録する</summary>
    public class NavigationArrowLifetimeScope : LifetimeScope
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private Transform _initialTarget;
        [SerializeField, Tooltip("ラジオ会話後に案内するFishingStageへのワープ地点")]
        [UnityEngine.Serialization.FormerlySerializedAs("_collectionTarget")]
        private Transform _fishingStageTarget;
        [SerializeField, Tooltip("初回釣果を持ち帰ったときに案内する無人集荷場の接近地点")]
        private Transform _firstCatchReturnTarget;
        [SerializeField] private Vector3 _initialTargetPosition = new Vector3(5f, 0f, 5f);

        /// <summary>案内に必要な初期値をServiceへ渡し、表示との接続を登録する</summary>
        /// <param name="builder">シーンの依存関係の登録先</param>
        /// <example>シーン読み込み時にVContainerが呼ぶ</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // ラジオ付近の歩行可能地点を案内先にする
            var targetPosition = _initialTarget != null ? _initialTarget.position : _initialTargetPosition;
            Vector3? fishingStagePosition = _fishingStageTarget != null ? _fishingStageTarget.position : null;
            Vector3? returnPosition = _firstCatchReturnTarget != null ? _firstCatchReturnTarget.position : null;
            builder.Register(_ => new NavigationArrowService(_playerTransform, targetPosition, fishingStagePosition,
                firstCatchReturnPosition: returnPosition), Lifetime.Singleton);

            // 親ScopeのTutorialControllerを参照してラジオからFishingStageのワープ地点へ切り替える
            builder.RegisterEntryPoint<NavigationArrowPresenter>();

            // 経路上から上昇するパーティクルの描画を登録する
            builder.RegisterComponentInHierarchy<NavigationArrowView>();
        }
    }
}
